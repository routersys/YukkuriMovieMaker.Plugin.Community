using System.Collections.Immutable;
using System.Numerics;
using System.Reflection.Metadata;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using D2DEffects = Vortice.Direct2D1.Effects;
using ProjectBlend = YukkuriMovieMaker.Project.Blend;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenShapeSource : IShapeSource
    {
        readonly DisposeCollector disposer = new();
        readonly InkStyleResourceManager inkStyleResourceManager = new();
        readonly SolidColorBrushManager solidColorBrushManager = new();

        readonly List<PenLayerRenderer> layerRenderers = [];
        readonly List<ID2D1CommandList> layerCommandLists = [];
        readonly List<IDisposable> compositionResources = [];

        List<PenLayerPlan> plans = [];
        List<PenLayerPlan> previousPlans = [];
        readonly List<bool> effectiveVisibility = [];

        readonly IGraphicsDevicesAndContext devices;
        readonly PenShapeParameter penShapeParameter;

        readonly ID2D1SolidColorBrush transparent;

        public ID2D1Image Output => outputImage ?? throw new NullReferenceException($"{nameof(outputImage)} is null.");
        ID2D1CommandList? commandList;
        ID2D1Image? outputImage;

        bool isEditing;
        double thickness;
        System.Drawing.Size screenSize;

        public PenShapeSource(IGraphicsDevicesAndContext devices, PenShapeParameter penShapeParameter)
        {
            this.devices = devices;
            this.penShapeParameter = penShapeParameter;
            disposer.Collect(inkStyleResourceManager);
            disposer.Collect(solidColorBrushManager);

            transparent = devices.DeviceContext.CreateSolidColorBrush(new Color4(0, 0, 0, 0));
            disposer.Collect(transparent);
        }


        public void Update(TimelineItemSourceDescription desc)
        {
            var dc = devices.DeviceContext;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var thickness = penShapeParameter.Thickness.GetValue(frame, length, fps);
            var isEditing = penShapeParameter.IsEditing;
            var screenSize = desc.ScreenSize;

            var layers = penShapeParameter.Layers;
            var isStrokesChanged = UpdateRenderers(layers);
            BuildPlans(layers, frame, length, fps);

            if (outputImage is not null
                && !isStrokesChanged
                && this.thickness == thickness
                && this.isEditing == isEditing
                && this.screenSize == screenSize
                && IsSamePlans())
                return;
            this.thickness = thickness;
            this.isEditing = isEditing;
            this.screenSize = screenSize;

            inkStyleResourceManager.BeginUse();
            solidColorBrushManager.BeginUse();

            ReleaseComposition();
            if (commandList is not null)
                disposer.RemoveAndDispose(ref commandList);
            commandList = dc.CreateCommandList();
            disposer.Collect(commandList);

            var isDirect = isEditing || IsDirectComposition();
            var transform = Matrix3x2.CreateTranslation(-screenSize.Width / 2f, -screenSize.Height / 2f);

            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);

            //1pxの透明を描画する
            //これがないとキャンバスが空の場合にcommandListの画面サイズが定まらず、エラーになる
            dc.DrawRectangle(new Vortice.RawRectF(0,0,1,1),transparent);

            if (isDirect && !isEditing)
            {
                dc.Transform = transform;
                foreach (var plan in plans)
                    layerRenderers[plan.Index].Draw(dc, plan.PointFrom, plan.PointLength, thickness, inkStyleResourceManager, solidColorBrushManager);
                dc.Transform = Matrix3x2.Identity;
            }
            dc.EndDraw();
            dc.Target = null;
            commandList.Close();

            outputImage = isDirect ? commandList : Compose(dc, commandList, transform, thickness);

            inkStyleResourceManager.EndUse();
            solidColorBrushManager.EndUse();

            (previousPlans, plans) = (plans, previousPlans);
        }

        bool UpdateRenderers(ImmutableList<PenLayer> layers)
        {
            var count = layers.IsEmpty ? 1 : layers.Count;

            while (layerRenderers.Count > count)
            {
                var last = layerRenderers.Count - 1;
                layerRenderers[last].Dispose();
                layerRenderers.RemoveAt(last);
            }
            while (layerRenderers.Count < count)
                layerRenderers.Add(new PenLayerRenderer());

            if (layers.IsEmpty)
                return layerRenderers[0].SetStrokes(penShapeParameter.Strokes);

            var isChanged = false;
            var index = 0;
            foreach (var layer in layers)
            {
                isChanged |= layerRenderers[index].SetStrokes(layer.Strokes);
                index++;
            }
            return isChanged;
        }

        void BuildPlans(ImmutableList<PenLayer> layers, int frame, int length, int fps)
        {
            plans.Clear();

            if (layers.IsEmpty)
            {
                GetRange(penShapeParameter.Length, penShapeParameter.Offset, layerRenderers[0].TotalPointCount, frame, length, fps, out var pointFrom, out var pointLength);
                plans.Add(new PenLayerPlan(0, pointFrom, pointLength, 100, ProjectBlend.Normal, false));
                return;
            }

            UpdateEffectiveVisibility(layers);

            var globalTotalPoints = 0;
            var index = 0;
            foreach (var layer in layers)
            {
                if (effectiveVisibility[index] && !layer.IsRangeOverridden)
                    globalTotalPoints += layerRenderers[index].TotalPointCount;
                index++;
            }
            GetRange(penShapeParameter.Length, penShapeParameter.Offset, globalTotalPoints, frame, length, fps, out var globalPointFrom, out var globalPointLength);

            var basePoint = 0;
            index = 0;
            foreach (var layer in layers)
            {
                if (!effectiveVisibility[index])
                {
                    index++;
                    continue;
                }

                int pointFrom;
                int pointLength;
                if (layer.IsRangeOverridden)
                {
                    GetRange(layer.Length, layer.Offset, layerRenderers[index].TotalPointCount, frame, length, fps, out pointFrom, out pointLength);
                }
                else
                {
                    pointFrom = globalPointFrom - basePoint;
                    pointLength = globalPointLength;
                    basePoint += layerRenderers[index].TotalPointCount;
                }
                plans.Add(new PenLayerPlan(index, pointFrom, pointLength, layer.Opacity.GetValue(frame, length, fps), layer.BlendMode, layer.IsClipping));
                index++;
            }
        }

        void UpdateEffectiveVisibility(ImmutableList<PenLayer> layers)
        {
            effectiveVisibility.Clear();

            PenLayer? clipBase = null;
            foreach (var layer in layers)
            {
                var isVisible = layer.IsVisible;
                if (layer.IsClipping && clipBase is not null && !clipBase.IsVisible)
                    isVisible = false;
                if (!layer.IsClipping)
                    clipBase = layer;
                effectiveVisibility.Add(isVisible);
            }
        }

        bool IsDirectComposition()
        {
            foreach (var plan in plans)
            {
                if (plan.Opacity < 100 || plan.BlendMode != ProjectBlend.Normal || plan.IsClipping)
                    return false;
            }
            return true;
        }

        ID2D1Image Compose(ID2D1DeviceContext6 dc, ID2D1CommandList baseImage, Matrix3x2 transform, double thickness)
        {
            foreach (var plan in plans)
            {
                var layerCommandList = dc.CreateCommandList();
                layerCommandLists.Add(layerCommandList);

                dc.Target = layerCommandList;
                dc.BeginDraw();
                dc.Clear(null);
                dc.Transform = transform;
                layerRenderers[plan.Index].Draw(dc, plan.PointFrom, plan.PointLength, thickness, inkStyleResourceManager, solidColorBrushManager);
                dc.Transform = Matrix3x2.Identity;
                dc.EndDraw();
                dc.Target = null;
                layerCommandList.Close();
            }

            ID2D1Effect? previous = null;
            ID2D1Image? clipBase = null;
            var index = 0;
            foreach (var plan in plans)
            {
                ID2D1Effect node;
                if (plan.BlendMode.IsCompositionEffect())
                {
                    var composite = new D2DEffects.Composite(dc) { InputCount = 2, Mode = plan.BlendMode.ToD2DCompositionMode() };
                    compositionResources.Add(composite);
                    node = composite;
                }
                else
                {
                    var blend = new D2DEffects.Blend(dc) { Mode = plan.BlendMode.ToD2DBlendMode() };
                    compositionResources.Add(blend);
                    node = blend;
                }

                if (previous is null)
                    node.SetInput(0, baseImage, true);
                else
                    node.SetInputEffect(0, previous);

                var layerImage = layerCommandLists[index];
                ID2D1Effect? sourceEffect = null;
                if (plan.IsClipping && clipBase is not null)
                {
                    var mask = new D2DEffects.AlphaMask(dc);
                    compositionResources.Add(mask);
                    mask.SetInput(0, layerImage, true);
                    mask.SetInput(1, clipBase, true);
                    sourceEffect = mask;
                }
                else
                {
                    clipBase = layerImage;
                }

                if (plan.Opacity < 100)
                {
                    var opacity = new D2DEffects.Opacity(dc) { Value = (float)(plan.Opacity / 100) };
                    compositionResources.Add(opacity);
                    if (sourceEffect is null)
                        opacity.SetInput(0, layerImage, true);
                    else
                        opacity.SetInputEffect(0, sourceEffect);
                    sourceEffect = opacity;
                }

                if (sourceEffect is null)
                    node.SetInput(1, layerImage, true);
                else
                    node.SetInputEffect(1, sourceEffect);

                previous = node;
                index++;
            }

            if (previous is null)
                return baseImage;

            var output = previous.Output;
            compositionResources.Add(output);
            return output;
        }

        void ReleaseComposition()
        {
            outputImage = null;
            for (var i = compositionResources.Count - 1; i >= 0; i--)
                compositionResources[i].Dispose();
            compositionResources.Clear();
            for (var i = layerCommandLists.Count - 1; i >= 0; i--)
                layerCommandLists[i].Dispose();
            layerCommandLists.Clear();
        }

        bool IsSamePlans()
        {
            if (plans.Count != previousPlans.Count)
                return false;
            for (var i = 0; i < plans.Count; i++)
            {
                if (!plans[i].Equals(previousPlans[i]))
                    return false;
            }
            return true;
        }

        static void GetRange(Animation lengthAnimation, Animation offsetAnimation, int totalPoints, int frame, int length, int fps, out int pointFrom, out int pointLength)
        {
            if (totalPoints <= 0)
            {
                pointFrom = 0;
                pointLength = 0;
                return;
            }

            var lengthRate = lengthAnimation.GetValue(frame, length, fps);
            var offset = offsetAnimation.GetValue(frame, length, fps);
            var doubleTotalPoints = totalPoints * 2;
            pointFrom = (int)((totalPoints * (offset + 100) / 100 % doubleTotalPoints + doubleTotalPoints) % doubleTotalPoints) - totalPoints;
            pointLength = (int)(totalPoints * lengthRate / 100);
        }

        #region IDisposable
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // マネージド状態を破棄します (マネージド オブジェクト)
                    ReleaseComposition();
                    foreach (var layerRenderer in layerRenderers)
                        layerRenderer.Dispose();
                    layerRenderers.Clear();
                    disposer.Dispose();
                }

                // アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~PenShapeSource()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}

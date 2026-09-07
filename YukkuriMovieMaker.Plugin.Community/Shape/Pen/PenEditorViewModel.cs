using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using FrameTime = YukkuriMovieMaker.Player.Video.FrameTime;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenEditorViewModel : Bindable, IDisposable
    {
        readonly DisposeCollector disposer = new();
        readonly IEditorInfo info;
        readonly ITimelineSourceAndDevices source;
        readonly PenShapeParameter document = new();
        readonly IShapeSource documentSource;
        readonly PenPreviewRenderer previewRenderer;
        readonly PenThumbnailRenderer thumbnailRenderer;
        readonly TimelineItemSourceDescription documentDescription;
        readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

        const float DefaultPressure = 0.5f;
        const int HistoryCapacity = 100;
        const int LassoPercentage = 80;
        const int ThumbnailWidth = 44;
        const int ThumbnailHeight = 26;

        readonly List<ImmutableList<PenLayer>> undoHistory = [];
        readonly List<ImmutableList<PenLayer>> redoHistory = [];
        ImmutableList<PenLayer> currentSnapshot = [];

        ImmutableList<int> selectionIndices = [];

        int layerNumber;
        int editDepth;
        bool isRenderQueued;
        bool isRestoring;
        bool isDirty;

        public double CanvasWidth { get; }

        public double CanvasHeight { get; }

        public BitmapSource BackgroundImage { get => backgroundImage; private set => Set(ref backgroundImage, value); }
        BitmapSource backgroundImage;

        public BitmapSource? DocumentImage { get => documentImage; private set => Set(ref documentImage, value); }
        BitmapSource? documentImage;

        public ImmutableList<PenLayer> Layers
        {
            get => document.Layers;
            set => SetLayers(value, value.IsEmpty ? null : value[^1]);
        }

        public ImmutableList<PenLayer> DisplayLayers { get => displayLayers; private set => Set(ref displayLayers, value); }
        ImmutableList<PenLayer> displayLayers = [];

        public PenLayer? ActiveLayer
        {
            get => activeLayer;
            set
            {
                if (!Set(ref activeLayer, value))
                    return;
                ClearSelection();
                UpdateCommands();
                OnPropertyChanged(nameof(IsLayerEditable));
            }
        }
        PenLayer? activeLayer;

        public Color WetInkColor { get => wetInkColor; private set => Set(ref wetInkColor, value); }
        Color wetInkColor = PenSettings.Default.PenStyle.StrokeColor;

        public double WetInkThickness { get => wetInkThickness; private set => Set(ref wetInkThickness, value); }
        double wetInkThickness = PenSettings.Default.PenStyle.StrokeThickness;

        public bool WetInkUsesPressure { get => wetInkUsesPressure; private set => Set(ref wetInkUsesPressure, value); }
        bool wetInkUsesPressure = true;

        public double StabilizationStrength { get => stabilizationStrength; private set => Set(ref stabilizationStrength, value); }
        double stabilizationStrength;

        public PenMode Mode { get => mode; private set => Set(ref mode, value); }
        PenMode mode = PenSettings.Default.PenMode is PenMode.Select ? PenMode.Pen : PenSettings.Default.PenMode;

        public Color StrokeColor
        {
            get => mode switch
            {
                PenMode.Highlighter => PenSettings.Default.HighlighterStyle.StrokeColor,
                PenMode.Eraser => Colors.Transparent,
                _ => PenSettings.Default.PenStyle.StrokeColor,
            };
            set
            {
                switch (mode)
                {
                    case PenMode.Highlighter:
                        PenSettings.Default.HighlighterStyle.StrokeColor = value;
                        break;
                    case PenMode.Eraser:
                        return;
                    default:
                        PenSettings.Default.PenStyle.StrokeColor = value;
                        break;
                }
                RefreshTool();
            }
        }

        public double StrokeThickness
        {
            get => mode switch
            {
                PenMode.Highlighter => PenSettings.Default.HighlighterStyle.StrokeThickness,
                PenMode.Eraser => PenSettings.Default.EraserStyle.StrokeThickness,
                _ => PenSettings.Default.PenStyle.StrokeThickness,
            };
            set
            {
                switch (mode)
                {
                    case PenMode.Highlighter:
                        PenSettings.Default.HighlighterStyle.StrokeThickness = value;
                        break;
                    case PenMode.Eraser:
                        PenSettings.Default.EraserStyle.StrokeThickness = value;
                        break;
                    default:
                        PenSettings.Default.PenStyle.StrokeThickness = value;
                        break;
                }
                RefreshTool();
            }
        }

        public ActionCommand ImportIsfCommand { get; }

        public ActionCommand ExportIsfCommand { get; }

        public ActionCommand SaveImageCommand { get; }

        public ActionCommand UndoCommand { get; }

        public ActionCommand RedoCommand { get; }

        public ActionCommand SelectPenCommand { get; }

        public ActionCommand SelectHighlighterCommand { get; }

        public ActionCommand SelectEraserCommand { get; }

        public ActionCommand SelectSelectionCommand { get; }

        public ActionCommand DeleteSelectionCommand { get; }

        public ActionCommand ClearSelectionCommand { get; }

        public bool IsLayerEditable => activeLayer is { IsLocked: false, IsVisible: true };

        public bool IsSelectionMode => mode is PenMode.Select;

        public Rect SelectionBounds { get => selectionBounds; private set => Set(ref selectionBounds, value); }
        Rect selectionBounds = Rect.Empty;

        public ActionCommand SelectEraserByPointCommand { get; }

        public ActionCommand SelectEraserByStrokeCommand { get; }

        public ActionCommand TogglePenPressure { get; }

        public ActionCommand ToggleHighlighterPressure { get; }

        public ActionCommand SetPenStabilizationCommand { get; }

        public ActionCommand SetHighlighterStabilizationCommand { get; }

        public ActionCommand AddLayerCommand { get; }

        public ActionCommand DuplicateLayerCommand { get; }

        public ActionCommand DeleteLayerCommand { get; }

        public ActionCommand MoveLayerUpCommand { get; }

        public ActionCommand MoveLayerDownCommand { get; }

        public PenEditorViewModel(IEditorInfo info, ImmutableList<PenLayer> layers, ImmutableList<SerializableStroke> strokes)
        {
            this.info = info;
            source = info.CreateTimelineVideoSource();
            disposer.Collect(source);

            CanvasWidth = info.VideoInfo.Width;
            CanvasHeight = info.VideoInfo.Height;

            previewRenderer = new PenPreviewRenderer(source.Devices);
            disposer.Collect(previewRenderer);
            thumbnailRenderer = new PenThumbnailRenderer(source.Devices);
            disposer.Collect(thumbnailRenderer);
            documentSource = document.CreateShapeSource(source.Devices);
            disposer.Collect(documentSource);

            var fps = info.VideoInfo.FPS;
            var screenSize = new System.Drawing.Size(info.VideoInfo.Width, info.VideoInfo.Height);
            var timelineDescription = new TimelineSourceDescription(
                screenSize,
                new FrameTime(0, fps),
                new FrameTime(1, fps),
                fps,
                TimelineSourceUsage.Paused,
                Guid.Empty,
                []);
            documentDescription = new TimelineItemSourceDescription(timelineDescription, 0, 1, 0);

            ImportIsfCommand = new ActionCommand(_ => true, _ => ImportIsf());
            ExportIsfCommand = new ActionCommand(_ => true, _ => ExportIsf());
            SaveImageCommand = new ActionCommand(_ => true, _ => SaveImage());

            UndoCommand = new ActionCommand(_ => undoHistory.Count > 0, _ => Undo());
            RedoCommand = new ActionCommand(_ => redoHistory.Count > 0, _ => Redo());

            SelectPenCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Pen));
            SelectHighlighterCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Highlighter));
            SelectEraserCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Eraser));
            SelectSelectionCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Select));
            DeleteSelectionCommand = new ActionCommand(_ => !selectionIndices.IsEmpty, _ => DeleteSelection());
            ClearSelectionCommand = new ActionCommand(_ => !selectionIndices.IsEmpty, _ => ClearSelection());
            SelectEraserByPointCommand = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.EraserStyle.Mode = EraserMode.Point;
                SelectMode(PenMode.Eraser);
            });
            SelectEraserByStrokeCommand = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.EraserStyle.Mode = EraserMode.Line;
                SelectMode(PenMode.Eraser);
            });
            TogglePenPressure = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.PenStyle.IsPressure = !PenSettings.Default.PenStyle.IsPressure;
                SelectMode(PenMode.Pen);
            });
            ToggleHighlighterPressure = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.HighlighterStyle.IsPressure = !PenSettings.Default.HighlighterStyle.IsPressure;
                SelectMode(PenMode.Highlighter);
            });
            SetPenStabilizationCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenStabilization value)
                    return;
                PenSettings.Default.PenStyle.Stabilization = value;
                SelectMode(PenMode.Pen);
            });
            SetHighlighterStabilizationCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenStabilization value)
                    return;
                PenSettings.Default.HighlighterStyle.Stabilization = value;
                SelectMode(PenMode.Highlighter);
            });

            AddLayerCommand = new ActionCommand(_ => true, _ => AddLayer());
            DuplicateLayerCommand = new ActionCommand(_ => activeLayer is not null, _ => DuplicateLayer());
            DeleteLayerCommand = new ActionCommand(_ => activeLayer is not null && document.Layers.Count > 1, _ => DeleteLayer());
            MoveLayerUpCommand = new ActionCommand(_ => CanMoveLayer(1), _ => MoveLayer(1));
            MoveLayerDownCommand = new ActionCommand(_ => CanMoveLayer(-1), _ => MoveLayer(-1));

            backgroundImage = BackgroundImage = RenderBackground();
            SetLayers(CreateInitialLayers(layers, strokes), null);
            RefreshTool();
            currentSnapshot = CaptureSnapshot();
            document.UndoRedoCommandCreated += OnDocumentChanged;
            UpdateDocumentImage();
        }

        public ImmutableList<SerializableStroke> CreateStrokeMirror()
        {
            var builder = ImmutableList.CreateBuilder<SerializableStroke>();
            foreach (var layer in document.Layers)
            {
                if (!layer.IsVisible)
                    continue;
                builder.AddRange(layer.Strokes);
            }
            return builder.ToImmutable();
        }

        void ImportIsf()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Ink Serialized Format|*.isf;",
                DefaultExt = ".isf",
            };
            if (dialog.ShowDialog() != true)
                return;

            StrokeCollection imported;
            using (var stream = new FileStream(dialog.FileName, FileMode.Open))
                imported = new StrokeCollection(stream);
            if (imported.Count == 0)
                return;

            var builder = ImmutableList.CreateBuilder<SerializableStroke>();
            foreach (var stroke in imported)
                builder.Add(new SerializableStroke(stroke));

            var layer = new PenLayer { Name = CreateLayerName(), Strokes = builder.ToImmutable() };
            var layers = document.Layers;
            var index = activeLayer is null ? layers.Count : layers.IndexOf(activeLayer) + 1;
            SetLayers(layers.Insert(index, layer), layer);
        }

        void ExportIsf()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Ink Serialized Format|*.isf;",
                DefaultExt = ".isf",
            };
            if (dialog.ShowDialog() != true)
                return;

            var strokes = new StrokeCollection();
            foreach (var serializable in CreateStrokeMirror())
                strokes.Add(serializable.ToStroke());

            using var stream = new FileStream(dialog.FileName, FileMode.Create);
            strokes.Save(stream);
        }

        void SaveImage()
        {
            if (documentImage is null)
                return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG|*.png;",
                DefaultExt = ".png",
            };
            if (dialog.ShowDialog() != true)
                return;

            var copy = new WriteableBitmap(documentImage);
            copy.Freeze();

            using var stream = new FileStream(dialog.FileName, FileMode.Create);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(copy));
            encoder.Save(stream);
        }

        ImmutableList<PenLayer> CreateInitialLayers(ImmutableList<PenLayer> layers, ImmutableList<SerializableStroke> strokes)
        {
            if (!layers.IsEmpty)
            {
                layerNumber = layers.Count;
                var builder = ImmutableList.CreateBuilder<PenLayer>();
                foreach (var layer in layers)
                    builder.Add(layer.Clone(layer.Id));
                return builder.ToImmutable();
            }

            return [new PenLayer { Name = CreateLayerName(), Strokes = strokes }];
        }

        public void BeginEditUnit()
        {
            editDepth++;
        }

        public void EndEditUnit()
        {
            if (editDepth > 0)
                editDepth--;
            if (editDepth == 0)
                CommitSnapshot();
        }

        void Undo()
        {
            if (undoHistory.Count == 0)
                return;

            var snapshot = undoHistory[^1];
            undoHistory.RemoveAt(undoHistory.Count - 1);
            redoHistory.Add(currentSnapshot);
            currentSnapshot = snapshot;
            Restore(snapshot);
        }

        void Redo()
        {
            if (redoHistory.Count == 0)
                return;

            var snapshot = redoHistory[^1];
            redoHistory.RemoveAt(redoHistory.Count - 1);
            undoHistory.Add(currentSnapshot);
            currentSnapshot = snapshot;
            Restore(snapshot);
        }

        ImmutableList<PenLayer> CaptureSnapshot()
        {
            var builder = ImmutableList.CreateBuilder<PenLayer>();
            foreach (var layer in document.Layers)
                builder.Add(layer.Clone(layer.Id));
            return builder.ToImmutable();
        }

        void Restore(ImmutableList<PenLayer> snapshot)
        {
            var builder = ImmutableList.CreateBuilder<PenLayer>();
            foreach (var layer in snapshot)
                builder.Add(layer.Clone(layer.Id));
            var layers = builder.ToImmutable();

            var activeId = activeLayer?.Id;
            var restoredActive = layers.Count == 0
                ? null
                : layers.FirstOrDefault(x => x.Id == activeId) ?? layers[^1];

            isRestoring = true;
            try
            {
                document.Layers = layers;
            }
            finally
            {
                isRestoring = false;
            }

            DisplayLayers = layers.Reverse();
            ActiveLayer = restoredActive;
            OnPropertyChanged(nameof(Layers));
            isDirty = false;
            UpdateCommands();
            UpdateHistoryCommands();
            InvalidateDocument();
        }

        void CommitSnapshot()
        {
            if (!isDirty)
                return;

            isDirty = false;
            undoHistory.Add(currentSnapshot);
            if (undoHistory.Count > HistoryCapacity)
                undoHistory.RemoveAt(0);
            redoHistory.Clear();
            currentSnapshot = CaptureSnapshot();
            UpdateHistoryCommands();
        }

        void UpdateHistoryCommands()
        {
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        void SelectMode(PenMode value)
        {
            PenSettings.Default.PenMode = value;
            Mode = value;
            ClearSelection();
            RefreshTool();
        }

        public void SelectByLasso(IReadOnlyList<Point> lassoPoints)
        {
            var layer = activeLayer;
            if (layer is null || lassoPoints.Count < 3)
            {
                ClearSelection();
                return;
            }

            var strokes = new StrokeCollection();
            foreach (var serializable in layer.Strokes)
                strokes.Add(serializable.ToStroke());

            var hits = strokes.HitTest(lassoPoints, LassoPercentage);
            if (hits.Count == 0)
            {
                ClearSelection();
                return;
            }

            var hitStrokes = new HashSet<Stroke>(hits);
            var indices = ImmutableList.CreateBuilder<int>();
            var bounds = Rect.Empty;
            for (var i = 0; i < strokes.Count; i++)
            {
                if (!hitStrokes.Contains(strokes[i]))
                    continue;
                indices.Add(i);
                bounds.Union(strokes[i].GetBounds());
            }

            selectionIndices = indices.ToImmutable();
            SelectionBounds = bounds;
            DeleteSelectionCommand.RaiseCanExecuteChanged();
            ClearSelectionCommand.RaiseCanExecuteChanged();
        }

        public void MoveSelection(Vector delta)
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || selectionIndices.IsEmpty)
                return;

            var builder = layer.Strokes.ToBuilder();
            foreach (var index in selectionIndices)
            {
                if (index >= builder.Count)
                    continue;

                var stroke = builder[index];
                var points = new SerializableStylusPoint[stroke.StylusPoints.Length];
                for (var i = 0; i < points.Length; i++)
                {
                    var point = stroke.StylusPoints[i];
                    points[i] = new SerializableStylusPoint(point.X + delta.X, point.Y + delta.Y, point.PressureFactor);
                }
                builder[index] = new SerializableStroke(points, stroke.DrawingAttributes);
            }

            layer.Strokes = builder.ToImmutable();
            SelectionBounds = Rect.Offset(selectionBounds, delta);
        }

        void DeleteSelection()
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || selectionIndices.IsEmpty)
                return;

            var builder = layer.Strokes.ToBuilder();
            for (var i = selectionIndices.Count - 1; i >= 0; i--)
            {
                var index = selectionIndices[i];
                if (index < builder.Count)
                    builder.RemoveAt(index);
            }

            layer.Strokes = builder.ToImmutable();
            ClearSelection();
        }

        void ClearSelection()
        {
            if (selectionIndices.IsEmpty && selectionBounds.IsEmpty)
                return;

            selectionIndices = [];
            SelectionBounds = Rect.Empty;
            DeleteSelectionCommand.RaiseCanExecuteChanged();
            ClearSelectionCommand.RaiseCanExecuteChanged();
        }

        void RefreshTool()
        {
            WetInkThickness = StrokeThickness;
            WetInkUsesPressure = mode is not PenMode.Eraser;
            StabilizationStrength = mode switch
            {
                PenMode.Pen => PenSettings.Default.PenStyle.Stabilization.ToStrength(),
                PenMode.Highlighter => PenSettings.Default.HighlighterStyle.Stabilization.ToStrength(),
                _ => 0,
            };
            WetInkColor = mode is PenMode.Eraser
                ? Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)
                : StrokeColor;
            OnPropertyChanged(nameof(StrokeColor));
            OnPropertyChanged(nameof(StrokeThickness));
            OnPropertyChanged(nameof(IsSelectionMode));
        }

        public void AddStroke(StylusPointCollection stylusPoints)
        {
            if (stylusPoints.Count == 0)
                return;

            var layer = activeLayer;
            if (!IsLayerEditable || layer is null)
                return;

            if (mode is PenMode.Eraser)
            {
                EraseStrokes(layer, stylusPoints);
                return;
            }

            var attributes = mode is PenMode.Highlighter
                ? PenStyleFactory.CreateHighlighter()
                : PenStyleFactory.CreatePen();
            if (attributes.IgnorePressure)
                NormalizePressure(stylusPoints);

            var stroke = new Stroke(stylusPoints, attributes);
            layer.Strokes = layer.Strokes.Add(new SerializableStroke(stroke));
        }

        static void NormalizePressure(StylusPointCollection stylusPoints)
        {
            for (var i = 0; i < stylusPoints.Count; i++)
            {
                var point = stylusPoints[i];
                stylusPoints[i] = new StylusPoint(point.X, point.Y, DefaultPressure);
            }
        }

        void EraseStrokes(PenLayer layer, StylusPointCollection stylusPoints)
        {
            var strokes = new StrokeCollection();
            foreach (var serializable in layer.Strokes)
                strokes.Add(serializable.ToStroke());

            var size = PenSettings.Default.EraserStyle.StrokeThickness;
            var shape = new EllipseStylusShape(size, size);
            var path = new List<Point>(stylusPoints.Count);
            foreach (var point in stylusPoints)
                path.Add(new Point(point.X, point.Y));

            if (PenSettings.Default.EraserStyle.Mode is EraserMode.Line)
            {
                var hits = strokes.HitTest(path, shape);
                if (hits.Count == 0)
                    return;
                foreach (var hit in hits)
                    strokes.Remove(hit);
            }
            else
            {
                strokes.Erase(path, shape);
            }

            var builder = ImmutableList.CreateBuilder<SerializableStroke>();
            foreach (var stroke in strokes)
                builder.Add(new SerializableStroke(stroke));
            layer.Strokes = builder.ToImmutable();
        }

        void AddLayer()
        {
            var layer = new PenLayer { Name = CreateLayerName() };
            var layers = document.Layers;
            var index = activeLayer is null ? layers.Count : layers.IndexOf(activeLayer) + 1;
            SetLayers(layers.Insert(index, layer), layer);
        }

        void DuplicateLayer()
        {
            var layer = activeLayer;
            if (layer is null)
                return;

            var copy = layer.Clone(Guid.NewGuid());
            copy.Name = CreateLayerName();

            var layers = document.Layers;
            SetLayers(layers.Insert(layers.IndexOf(layer) + 1, copy), copy);
        }

        void DeleteLayer()
        {
            var layer = activeLayer;
            if (layer is null)
                return;

            var layers = document.Layers;
            var index = layers.IndexOf(layer);
            if (index < 0 || layers.Count <= 1)
                return;

            var next = layers.RemoveAt(index);
            SetLayers(next, next[Math.Min(index, next.Count - 1)]);
        }

        bool CanMoveLayer(int delta)
        {
            if (activeLayer is null)
                return false;

            var index = document.Layers.IndexOf(activeLayer);
            var target = index + delta;
            return index >= 0 && target >= 0 && target < document.Layers.Count;
        }

        void MoveLayer(int delta)
        {
            var layer = activeLayer;
            if (layer is null)
                return;

            var layers = document.Layers;
            var index = layers.IndexOf(layer);
            var target = index + delta;
            if (index < 0 || target < 0 || target >= layers.Count)
                return;

            SetLayers(layers.RemoveAt(index).Insert(target, layer), layer);
        }

        void SetLayers(ImmutableList<PenLayer> layers, PenLayer? active)
        {
            document.Layers = layers;
            DisplayLayers = layers.Reverse();
            ActiveLayer = active ?? (layers.IsEmpty ? null : layers[^1]);
            OnPropertyChanged(nameof(Layers));
            UpdateCommands();
            InvalidateDocument();
        }

        string CreateLayerName()
        {
            layerNumber++;
            return string.Format(Texts.LayerDefaultName, layerNumber);
        }

        void UpdateCommands()
        {
            DuplicateLayerCommand.RaiseCanExecuteChanged();
            DeleteLayerCommand.RaiseCanExecuteChanged();
            MoveLayerUpCommand.RaiseCanExecuteChanged();
            MoveLayerDownCommand.RaiseCanExecuteChanged();
        }

        void OnDocumentChanged(object? sender, YukkuriMovieMaker.UndoRedo.UndoRedoEventArgs e)
        {
            if (isRestoring)
                return;

            isDirty = true;
            if (editDepth == 0)
                CommitSnapshot();
            OnPropertyChanged(nameof(IsLayerEditable));
            InvalidateDocument();
        }

        void InvalidateDocument()
        {
            if (isRenderQueued)
                return;

            isRenderQueued = true;
            dispatcher.InvokeAsync(() =>
            {
                isRenderQueued = false;
                UpdateDocumentImage();
            }, DispatcherPriority.Render);
        }

        void UpdateDocumentImage()
        {
            documentSource.Update(documentDescription);
            DocumentImage = previewRenderer.Render(documentSource.Output, info.VideoInfo.Width, info.VideoInfo.Height);
            thumbnailRenderer.Update(document.Layers, ThumbnailWidth, ThumbnailHeight, CanvasWidth, CanvasHeight);
        }

        BitmapSource RenderBackground()
        {
            var time = info.ItemPosition.Time < TimeSpan.Zero
                ? info.ItemPosition.Time
                : info.ItemDuration.Time < info.ItemPosition.Time
                ? info.VideoInfo.GetTimeFrom(info.ItemPosition.Frame + info.ItemDuration.Frame - 1)
                : info.TimelinePosition.Time;
            source.Update(time, TimelineSourceUsage.Paused);
            return source.RenderBitmapSource();
        }

        public void Dispose()
        {
            document.UndoRedoCommandCreated -= OnDocumentChanged;
            disposer.Dispose();
        }
    }
}

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using FrameTime = YukkuriMovieMaker.Player.Video.FrameTime;
using ProjectBlend = YukkuriMovieMaker.Project.Blend;

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

        const int HistoryCapacity = 100;
        const int LassoPercentage = 80;
        const int ThumbnailWidth = 44;
        const int ThumbnailHeight = 26;
        const double MinStylusSize = 3.77952755905512E-05;
        const string ClipboardFormat = "YukkuriMovieMaker.Plugin.Community.Shape.Pen.Strokes";
        const double MaxStylusSize = 162329.461417323;

        static readonly Color EraserWetInkColor = Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF);

        readonly PenFillEngine fillEngine = new();

        readonly List<ImmutableList<PenLayer>> undoHistory = [];
        readonly List<ImmutableList<PenLayer>> redoHistory = [];
        ImmutableList<PenLayer> currentSnapshot = [];

        ImmutableList<int> selectionIndices = [];
        ImmutableList<SerializableStroke>? transformSource;
        PenLayer? transformLayer;

        int layerNumber;
        int folderNumber;
        int editDepth;
        bool isRenderQueued;
        bool isDisposed;
        bool isRestoring;
        bool isDirty;

        public double CanvasWidth { get; }

        public double CanvasHeight { get; }

        public BitmapSource BackgroundImage { get => backgroundImage; private set => Set(ref backgroundImage, value); }
        BitmapSource backgroundImage;

        public BitmapSource? DocumentImage { get => documentImage; private set => Set(ref documentImage, value); }
        BitmapSource? documentImage;

        public ImmutableList<PenLayer> Layers => document.Layers;

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
                OnPropertyChanged(nameof(IsRangeSupported));
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

        public bool IgnoresPressure { get => ignoresPressure; private set => Set(ref ignoresPressure, value); }
        bool ignoresPressure;

        public double TaperLength { get => taperLength; private set => Set(ref taperLength, value); }
        double taperLength;

        public PenMode Mode { get => mode; private set => Set(ref mode, value); }
        PenMode mode = PenSettings.Default.PenMode is PenMode.Select ? PenMode.Pen : PenSettings.Default.PenMode;

        public Color StrokeColor
        {
            get => mode switch
            {
                PenMode.Highlighter => PenSettings.Default.HighlighterStyle.StrokeColor,
                PenMode.Fill => PenSettings.Default.FillStyle.StrokeColor,
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
                    case PenMode.Fill:
                        PenSettings.Default.FillStyle.StrokeColor = value;
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

        public ActionCommand SelectFillCommand { get; }

        public ActionCommand SelectByLassoCommand { get; }

        public ActionCommand SelectByRectangleCommand { get; }

        public ActionCommand DeleteSelectionCommand { get; }

        public ActionCommand ClearSelectionCommand { get; }

        public ActionCommand SelectAllCommand { get; }

        public ActionCommand DuplicateSelectionCommand { get; }

        public ActionCommand SelectionToNewLayerCommand { get; }

        public ActionCommand ApplySelectionColorCommand { get; }

        public ActionCommand ApplySelectionThicknessCommand { get; }

        public ActionCommand CutSelectionCommand { get; }

        public ActionCommand CopySelectionCommand { get; }

        public ActionCommand PasteCommand { get; }

        public bool IsLayerEditable => activeLayer is { IsLocked: false, IsVisible: true, IsFolder: false };

        public bool IsRangeSupported => activeLayer is { IsFolder: false };

        public bool IsSelectionMode => mode is PenMode.Select;

        public bool IsFillMode => mode is PenMode.Fill;

        public bool IsRectangleSelection => PenSettings.Default.SelectionKind is PenSelectionKind.Rectangle;

        public Rect SelectionBounds { get => selectionBounds; private set => Set(ref selectionBounds, value); }
        Rect selectionBounds = Rect.Empty;

        public ActionCommand SelectEraserByPointCommand { get; }

        public ActionCommand SelectEraserByStrokeCommand { get; }

        public ActionCommand TogglePenPressure { get; }

        public ActionCommand ToggleHighlighterPressure { get; }

        public ActionCommand SetPenStabilizationCommand { get; }

        public ActionCommand SetHighlighterStabilizationCommand { get; }

        public ActionCommand SetPenTaperCommand { get; }

        public ActionCommand SetHighlighterTaperCommand { get; }

        public ActionCommand SetFillToleranceCommand { get; }

        public ActionCommand AddLayerCommand { get; }

        public ActionCommand AddFolderCommand { get; }

        public ActionCommand MoveIntoFolderCommand { get; }

        public ActionCommand MoveOutOfFolderCommand { get; }

        public ActionCommand ToggleExpandCommand { get; }

        public ActionCommand DuplicateLayerCommand { get; }

        public ActionCommand DeleteLayerCommand { get; }

        public ActionCommand MergeLayerCommand { get; }

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

            UndoCommand = new ActionCommand(_ => editDepth == 0 && undoHistory.Count > 0, _ => Undo());
            RedoCommand = new ActionCommand(_ => editDepth == 0 && redoHistory.Count > 0, _ => Redo());

            SelectPenCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Pen));
            SelectHighlighterCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Highlighter));
            SelectEraserCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Eraser));
            SelectSelectionCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Select));
            SelectFillCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Fill));
            SelectByLassoCommand = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.SelectionKind = PenSelectionKind.Lasso;
                SelectMode(PenMode.Select);
            });
            SelectByRectangleCommand = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.SelectionKind = PenSelectionKind.Rectangle;
                SelectMode(PenMode.Select);
            });
            DeleteSelectionCommand = new ActionCommand(_ => editDepth == 0 && !selectionIndices.IsEmpty, _ => DeleteSelection());
            ClearSelectionCommand = new ActionCommand(_ => editDepth == 0 && !selectionIndices.IsEmpty, _ => ClearSelection());
            SelectAllCommand = new ActionCommand(_ => editDepth == 0 && IsLayerEditable, _ => SelectAll());
            DuplicateSelectionCommand = new ActionCommand(_ => editDepth == 0 && !selectionIndices.IsEmpty, _ => DuplicateSelection());
            SelectionToNewLayerCommand = new ActionCommand(_ => editDepth == 0 && !selectionIndices.IsEmpty, _ => MoveSelectionToNewLayer());
            ApplySelectionColorCommand = new ActionCommand(_ => editDepth == 0 && !selectionIndices.IsEmpty, _ => ApplySelectionColor());
            ApplySelectionThicknessCommand = new ActionCommand(_ => editDepth == 0 && !selectionIndices.IsEmpty, _ => ApplySelectionThickness());
            CutSelectionCommand = new ActionCommand(_ => editDepth == 0 && !selectionIndices.IsEmpty, _ => CutSelection());
            CopySelectionCommand = new ActionCommand(_ => editDepth == 0 && !selectionIndices.IsEmpty, _ => CopySelection());
            PasteCommand = new ActionCommand(_ => editDepth == 0 && IsLayerEditable && Clipboard.ContainsData(ClipboardFormat), _ => Paste());
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
            SetPenTaperCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenTaper value)
                    return;
                PenSettings.Default.PenStyle.Taper = value;
                SelectMode(PenMode.Pen);
            });
            SetHighlighterTaperCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenTaper value)
                    return;
                PenSettings.Default.HighlighterStyle.Taper = value;
                SelectMode(PenMode.Highlighter);
            });
            SetFillToleranceCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenFillTolerance value)
                    return;
                PenSettings.Default.FillStyle.Tolerance = value;
                SelectMode(PenMode.Fill);
            });

            AddLayerCommand = new ActionCommand(_ => true, _ => AddLayer());
            AddFolderCommand = new ActionCommand(_ => true, _ => AddFolder());
            MoveIntoFolderCommand = new ActionCommand(_ => FindTargetFolder() is not null, _ => MoveIntoFolder());
            MoveOutOfFolderCommand = new ActionCommand(_ => activeLayer is { } layer && layer.ParentId != Guid.Empty, _ => MoveOutOfFolder());
            ToggleExpandCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenLayer layer || !layer.IsFolder)
                    return;

                layer.IsExpanded = !layer.IsExpanded;
                if (activeLayer is not null && IsCollapsed(document.Layers, activeLayer))
                    ActiveLayer = layer;
                UpdateDisplayLayers(document.Layers);
            });
            DuplicateLayerCommand = new ActionCommand(_ => activeLayer is not null, _ => DuplicateLayer());
            DeleteLayerCommand = new ActionCommand(_ => CanDeleteLayer(), _ => DeleteLayer());
            MergeLayerCommand = new ActionCommand(_ => FindMergeTarget() is not null, _ => MergeLayer());
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
            var layers = document.Layers;
            var builder = ImmutableList.CreateBuilder<SerializableStroke>();
            foreach (var layer in layers)
            {
                if (!layer.IsVisible || IsHiddenByFolder(layers, layer))
                    continue;
                builder.AddRange(layer.Strokes);
            }
            return builder.ToImmutable();
        }

        static bool IsHiddenByFolder(ImmutableList<PenLayer> layers, PenLayer layer)
        {
            var parentId = layer.ParentId;
            while (parentId != Guid.Empty)
            {
                var parent = FindLayer(layers, parentId);
                if (parent is null)
                    return false;
                if (!parent.IsVisible)
                    return true;
                parentId = parent.ParentId;
            }
            return false;
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

            ImportIsf(dialog.FileName);
        }

        void ImportIsf(string path)
        {
            StrokeCollection imported;
            using (var stream = new FileStream(path, FileMode.Open))
                imported = new StrokeCollection(stream);
            if (imported.Count == 0)
                return;

            var builder = ImmutableList.CreateBuilder<SerializableStroke>();
            foreach (var stroke in imported)
                builder.Add(CreateStroke(stroke));

            var layer = new PenLayer { Name = CreateLayerName(), Strokes = builder.ToImmutable(), ParentId = activeLayer?.ParentId ?? Guid.Empty };
            var layers = document.Layers;
            var index = activeLayer is null ? layers.Count : layers.IndexOf(activeLayer) + 1;
            SetLayers(Normalize(layers.Insert(index, layer)), layer);
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

            ExportIsf(dialog.FileName);
        }

        void ExportIsf(string path)
        {
            var strokes = new StrokeCollection();
            foreach (var serializable in CreateStrokeMirror())
                strokes.Add(CreateIsfStroke(serializable));

            using var stream = new FileStream(path, FileMode.Create);
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
                folderNumber = layers.Count;
                var builder = ImmutableList.CreateBuilder<PenLayer>();
                foreach (var layer in layers)
                    builder.Add(layer.Clone(layer.Id));
                return Normalize(builder.ToImmutable());
            }

            return [new PenLayer { Name = CreateLayerName(), Strokes = strokes }];
        }

        public void BeginEditUnit()
        {
            editDepth++;
            if (editDepth == 1)
                UpdateGestureCommands();
        }

        public void EndEditUnit()
        {
            if (editDepth > 0)
                editDepth--;
            if (editDepth > 0)
                return;

            CommitSnapshot();
            UpdateGestureCommands();
        }

        void UpdateGestureCommands()
        {
            UpdateHistoryCommands();
            UpdateSelectionCommands();
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
            var layers = Normalize(builder.ToImmutable());

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

            if (restoredActive is not null)
                ExpandAncestors(layers, restoredActive);
            UpdateDisplayLayers(layers);
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
            for (var i = 0; i < strokes.Count; i++)
            {
                if (hitStrokes.Contains(strokes[i]))
                    indices.Add(i);
            }

            SetSelection(indices.ToImmutable(), layer.Strokes);
        }

        void SetSelection(ImmutableList<int> indices, ImmutableList<SerializableStroke> strokes)
        {
            selectionIndices = indices;
            SelectionBounds = GetSelectionBounds(strokes);
            UpdateSelectionCommands();
        }

        void SelectAll()
        {
            SelectMode(PenMode.Select);

            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || layer.Strokes.IsEmpty)
                return;

            var indices = ImmutableList.CreateBuilder<int>();
            for (var i = 0; i < layer.Strokes.Count; i++)
                indices.Add(i);
            SetSelection(indices.ToImmutable(), layer.Strokes);
        }

        void DuplicateSelection()
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || selectionIndices.IsEmpty)
                return;

            var source = layer.Strokes;
            var builder = source.ToBuilder();
            var indices = ImmutableList.CreateBuilder<int>();
            foreach (var index in selectionIndices)
            {
                if (index >= source.Count)
                    continue;

                indices.Add(builder.Count);
                builder.Add(source[index]);
            }

            layer.Strokes = builder.ToImmutable();
            SetSelection(indices.ToImmutable(), layer.Strokes);
        }

        void MoveSelectionToNewLayer()
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || selectionIndices.IsEmpty)
                return;

            BeginEditUnit();

            var moved = ImmutableList.CreateBuilder<SerializableStroke>();
            var remaining = layer.Strokes.ToBuilder();
            for (var i = selectionIndices.Count - 1; i >= 0; i--)
            {
                var index = selectionIndices[i];
                if (index >= remaining.Count)
                    continue;

                moved.Insert(0, remaining[index]);
                remaining.RemoveAt(index);
            }

            layer.Strokes = remaining.ToImmutable();
            ClearSelection();

            var created = new PenLayer { Name = CreateLayerName(), ParentId = layer.ParentId, Strokes = moved.ToImmutable() };
            var layers = document.Layers;
            SetLayers(Normalize(layers.Insert(layers.IndexOf(layer) + 1, created)), created);

            EndEditUnit();
        }

        public void BeginSelectionTransform()
        {
            BeginEditUnit();

            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || selectionIndices.IsEmpty)
                return;

            transformSource = layer.Strokes;
            transformLayer = layer;
        }

        public void TransformSelection(Matrix matrix)
        {
            var source = transformSource;
            var layer = transformLayer;
            if (source is null || layer is null || selectionIndices.IsEmpty || !ReferenceEquals(layer, activeLayer))
                return;

            var scale = Math.Sqrt(Math.Abs(matrix.Determinant));
            var builder = source.ToBuilder();
            foreach (var index in selectionIndices)
            {
                if (index >= source.Count)
                    continue;

                var stroke = source[index];
                var points = new SerializableStylusPoint[stroke.StylusPoints.Length];
                for (var i = 0; i < points.Length; i++)
                {
                    var point = stroke.StylusPoints[i];
                    var moved = matrix.Transform(new Point(point.X, point.Y));
                    points[i] = new SerializableStylusPoint(moved.X, moved.Y, point.PressureFactor);
                }
                builder[index] = new SerializableStroke(points, ScaleAttributes(stroke.DrawingAttributes, scale)) { FillFigures = stroke.FillFigures };
            }

            layer.Strokes = builder.ToImmutable();
            SelectionBounds = GetSelectionBounds(layer.Strokes);
        }

        public void EndSelectionTransform()
        {
            transformSource = null;
            transformLayer = null;
            EndEditUnit();
        }

        static DrawingAttributes ScaleAttributes(DrawingAttributes attributes, double scale)
        {
            if (scale == 1)
                return attributes;

            var scaled = attributes.Clone();
            scaled.Width = ClampStylusSize(attributes.Width * scale);
            scaled.Height = ClampStylusSize(attributes.Height * scale);
            return scaled;
        }

        static double ClampStylusSize(double size)
        {
            if (double.IsNaN(size) || size < MinStylusSize)
                return MinStylusSize;
            return size > MaxStylusSize ? MaxStylusSize : size;
        }

        Rect GetSelectionBounds(ImmutableList<SerializableStroke> strokes)
        {
            var bounds = Rect.Empty;
            foreach (var index in selectionIndices)
            {
                if (index < strokes.Count)
                    bounds.Union(strokes[index].ToStroke().GetBounds());
            }
            return bounds;
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

        void ApplySelectionColor()
        {
            var color = StrokeColor;
            ApplyToSelection(stroke =>
            {
                var attributes = stroke.DrawingAttributes;
                if (attributes.Color == color)
                    return null;

                var changed = attributes.Clone();
                changed.Color = color;
                return changed;
            });
        }

        void ApplySelectionThickness()
        {
            var thickness = StrokeThickness;
            ApplyToSelection(stroke =>
            {
                if (stroke.FillFigures is not null)
                    return null;

                var attributes = stroke.DrawingAttributes;
                var height = attributes.Height;
                if (height <= 0)
                    return null;

                var width = ClampStylusSize(thickness * attributes.Width / height);
                var scaled = ClampStylusSize(thickness);
                if (attributes.Height == scaled && attributes.Width == width)
                    return null;

                var changed = attributes.Clone();
                changed.Width = width;
                changed.Height = scaled;
                return changed;
            });
        }

        void ApplyToSelection(Func<SerializableStroke, DrawingAttributes?> convert)
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || selectionIndices.IsEmpty)
                return;

            var builder = layer.Strokes.ToBuilder();
            var isChanged = false;
            foreach (var index in selectionIndices)
            {
                if (index >= builder.Count)
                    continue;

                var stroke = builder[index];
                var attributes = convert(stroke);
                if (attributes is null)
                    continue;

                builder[index] = new SerializableStroke(stroke.StylusPoints, attributes) { FillFigures = stroke.FillFigures };
                isChanged = true;
            }

            if (!isChanged)
                return;

            layer.Strokes = builder.ToImmutable();
            SetSelection(selectionIndices, layer.Strokes);
        }

        static Stroke CreateIsfStroke(SerializableStroke serializable)
        {
            var stroke = serializable.ToStroke();
            if (serializable.FillFigures is { } figures)
                stroke.AddPropertyData(PenFillFigures.PropertyId, figures);
            return stroke;
        }

        static SerializableStroke CreateStroke(Stroke stroke)
        {
            var figures = stroke.ContainsPropertyData(PenFillFigures.PropertyId)
                ? stroke.GetPropertyData(PenFillFigures.PropertyId) as int[]
                : null;
            return new SerializableStroke(stroke) { FillFigures = figures };
        }

        void CutSelection()
        {
            if (!IsLayerEditable || activeLayer is null || selectionIndices.IsEmpty)
                return;

            CopySelection();
            DeleteSelection();
        }

        void CopySelection()
        {
            var layer = activeLayer;
            if (layer is null || selectionIndices.IsEmpty)
                return;

            var items = new List<SerializableStroke>(selectionIndices.Count);
            foreach (var index in selectionIndices)
            {
                if (index < layer.Strokes.Count)
                    items.Add(layer.Strokes[index]);
            }
            if (items.Count == 0)
                return;

            Clipboard.SetData(ClipboardFormat, JsonConvert.SerializeObject(items));
        }

        void Paste()
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null)
                return;
            if (Clipboard.GetData(ClipboardFormat) is not string text)
                return;
            if (JsonConvert.DeserializeObject<List<SerializableStroke>>(text) is not { Count: > 0 } items)
                return;

            var builder = layer.Strokes.ToBuilder();
            var indices = ImmutableList.CreateBuilder<int>();
            foreach (var item in items)
            {
                indices.Add(builder.Count);
                builder.Add(item);
            }

            layer.Strokes = builder.ToImmutable();
            SelectMode(PenMode.Select);
            SetSelection(indices.ToImmutable(), layer.Strokes);
        }

        void ClearSelection()
        {
            if (selectionIndices.IsEmpty && selectionBounds.IsEmpty)
                return;

            selectionIndices = [];
            SelectionBounds = Rect.Empty;
            UpdateSelectionCommands();
        }

        void UpdateSelectionCommands()
        {
            DeleteSelectionCommand.RaiseCanExecuteChanged();
            ClearSelectionCommand.RaiseCanExecuteChanged();
            DuplicateSelectionCommand.RaiseCanExecuteChanged();
            SelectionToNewLayerCommand.RaiseCanExecuteChanged();
            ApplySelectionColorCommand.RaiseCanExecuteChanged();
            ApplySelectionThicknessCommand.RaiseCanExecuteChanged();
            CutSelectionCommand.RaiseCanExecuteChanged();
            CopySelectionCommand.RaiseCanExecuteChanged();
            PasteCommand.RaiseCanExecuteChanged();
            SelectAllCommand.RaiseCanExecuteChanged();
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
            IgnoresPressure = mode switch
            {
                PenMode.Pen => !PenSettings.Default.PenStyle.IsPressure,
                PenMode.Highlighter => !PenSettings.Default.HighlighterStyle.IsPressure,
                _ => true,
            };
            TaperLength = mode switch
            {
                PenMode.Pen => PenSettings.Default.PenStyle.Taper.ToLength(StrokeThickness),
                PenMode.Highlighter => PenSettings.Default.HighlighterStyle.Taper.ToLength(StrokeThickness),
                _ => 0,
            };
            WetInkColor = mode is PenMode.Eraser ? EraserWetInkColor : StrokeColor;
            OnPropertyChanged(nameof(StrokeColor));
            OnPropertyChanged(nameof(StrokeThickness));
            OnPropertyChanged(nameof(IsSelectionMode));
            OnPropertyChanged(nameof(IsFillMode));
            OnPropertyChanged(nameof(IsRectangleSelection));
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
            var stroke = new Stroke(stylusPoints, attributes);
            layer.Strokes = layer.Strokes.Add(new SerializableStroke(stroke));
        }

        public void Fill(Point point)
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null)
                return;

            UpdateDocumentImage();
            if (documentImage is not WriteableBitmap image)
                return;

            var difference = PenSettings.Default.FillStyle.Tolerance.ToDifference();
            if (!fillEngine.TryFill(image, CreateStrokeMirror(), point, difference, out var points, out var figures))
                return;

            layer.Strokes = layer.Strokes.Add(new SerializableStroke(points, PenStyleFactory.CreateFill()) { FillFigures = figures });
        }

        void EraseStrokes(PenLayer layer, StylusPointCollection stylusPoints)
        {
            var size = PenSettings.Default.EraserStyle.StrokeThickness;
            var shape = new EllipseStylusShape(size, size);
            var path = new List<Point>(stylusPoints.Count);
            foreach (var point in stylusPoints)
                path.Add(new Point(point.X, point.Y));

            var isLine = PenSettings.Default.EraserStyle.Mode is EraserMode.Line;
            var builder = ImmutableList.CreateBuilder<SerializableStroke>();
            var isErased = false;
            foreach (var serializable in layer.Strokes)
            {
                var isFill = serializable.FillFigures is not null;
                var stroke = serializable.ToStroke();
                if (!(isFill ? IsFillErased(serializable, stroke, path, shape) : stroke.HitTest(path, shape)))
                {
                    builder.Add(serializable);
                    continue;
                }

                isErased = true;
                if (isLine || isFill)
                    continue;

                foreach (var erased in stroke.GetEraseResult(path, shape))
                    builder.Add(new SerializableStroke(erased));
            }

            if (!isErased)
                return;

            layer.Strokes = builder.ToImmutable();
        }

        static bool IsFillErased(SerializableStroke serializable, Stroke stroke, List<Point> path, StylusShape shape)
        {
            if (stroke.HitTest(path, shape))
                return true;

            var geometry = PenFillFigures.CreateGeometry(serializable);
            foreach (var point in path)
            {
                if (geometry.FillContains(point))
                    return true;
            }
            return false;
        }

        void AddLayer()
        {
            var layer = new PenLayer { Name = CreateLayerName(), ParentId = activeLayer?.ParentId ?? Guid.Empty };
            var layers = document.Layers;
            var index = activeLayer is null ? layers.Count : layers.IndexOf(activeLayer) + 1;
            SetLayers(Normalize(layers.Insert(index, layer)), layer);
        }

        void AddFolder()
        {
            var folder = new PenLayer { Name = CreateFolderName(), IsFolder = true, ParentId = activeLayer?.ParentId ?? Guid.Empty };
            var layers = document.Layers;
            var index = activeLayer is null ? layers.Count : layers.IndexOf(activeLayer) + 1;
            SetLayers(Normalize(layers.Insert(index, folder)), folder);
        }

        PenLayer? FindTargetFolder()
        {
            var layer = activeLayer;
            if (layer is null)
                return null;

            var layers = document.Layers;
            var index = layers.IndexOf(layer);
            if (index < 0)
                return null;

            for (var i = index + 1; i < layers.Count; i++)
            {
                var candidate = layers[i];
                if (candidate.ParentId != layer.ParentId)
                    continue;
                return candidate.IsFolder ? candidate : null;
            }
            return null;
        }

        void MoveIntoFolder()
        {
            var layer = activeLayer;
            var folder = FindTargetFolder();
            if (layer is null || folder is null)
                return;

            BeginEditUnit();
            layer.ParentId = folder.Id;
            SetLayers(Normalize(document.Layers), layer);
            EndEditUnit();
        }

        void MoveOutOfFolder()
        {
            var layer = activeLayer;
            if (layer is null || layer.ParentId == Guid.Empty)
                return;

            var layers = document.Layers;
            var folder = FindLayer(layers, layer.ParentId);
            if (folder is null)
                return;

            BeginEditUnit();
            layer.ParentId = folder.ParentId;
            var next = layers.Remove(layer);
            SetLayers(Normalize(next.Insert(next.IndexOf(folder) + 1, layer)), layer);
            EndEditUnit();
        }

        void DuplicateLayer()
        {
            var layer = activeLayer;
            if (layer is null)
                return;

            var layers = document.Layers;
            var copies = ImmutableList.CreateBuilder<PenLayer>();
            var copy = CloneSubtree(layers, layer, layer.ParentId, copies);
            copy.Name = layer.IsFolder ? CreateFolderName() : CreateLayerName();

            SetLayers(Normalize(layers.InsertRange(layers.IndexOf(layer) + 1, copies)), copy);
        }

        static PenLayer CloneSubtree(ImmutableList<PenLayer> layers, PenLayer layer, Guid parentId, ImmutableList<PenLayer>.Builder builder)
        {
            var copy = layer.Clone(Guid.NewGuid());
            copy.ParentId = parentId;
            if (layer.IsFolder)
            {
                foreach (var child in layers)
                {
                    if (child.ParentId == layer.Id)
                        CloneSubtree(layers, child, copy.Id, builder);
                }
            }
            builder.Add(copy);
            return copy;
        }

        void DeleteLayer()
        {
            var layer = activeLayer;
            if (layer is null)
                return;

            var layers = document.Layers;
            var index = layers.IndexOf(layer);
            if (index < 0)
                return;

            var builder = layers.ToBuilder();
            RemoveSubtree(builder, layer);
            if (builder.Count == 0)
                return;

            var next = Normalize(builder.ToImmutable());
            SetLayers(next, next[Math.Min(index, next.Count - 1)]);
        }

        static void RemoveSubtree(ImmutableList<PenLayer>.Builder builder, PenLayer layer)
        {
            if (layer.IsFolder)
            {
                for (var i = builder.Count - 1; i >= 0; i--)
                {
                    if (builder[i].ParentId == layer.Id)
                        RemoveSubtree(builder, builder[i]);
                }
            }
            builder.Remove(layer);
        }

        int CountSubtree(PenLayer layer)
        {
            var count = 1;
            if (!layer.IsFolder)
                return count;

            foreach (var candidate in document.Layers)
            {
                if (candidate.ParentId == layer.Id)
                    count += CountSubtree(candidate);
            }
            return count;
        }

        PenLayer? FindMergeTarget()
        {
            var layer = activeLayer;
            if (layer is null || !IsMergeable(layer))
                return null;

            var lower = FindSibling(document.Layers, layer, -1);
            if (lower is null || !IsMergeable(lower))
                return null;

            return HasFill(layer) && HasStroke(lower) ? null : lower;
        }

        static bool HasFill(PenLayer layer)
        {
            foreach (var stroke in layer.Strokes)
            {
                if (stroke.FillFigures is not null)
                    return true;
            }
            return false;
        }

        static bool HasStroke(PenLayer layer)
        {
            foreach (var stroke in layer.Strokes)
            {
                if (stroke.FillFigures is null)
                    return true;
            }
            return false;
        }

        static bool IsMergeable(PenLayer layer)
        {
            if (layer is not { IsFolder: false, IsVisible: true, IsLocked: false, IsClipping: false, IsRangeOverridden: false })
                return false;
            if (layer.BlendMode is not ProjectBlend.Normal)
                return false;

            var values = layer.Opacity.Values;
            return values.Count == 1 && values[0].Value == 100;
        }

        void MergeLayer()
        {
            var layer = activeLayer;
            var lower = FindMergeTarget();
            if (layer is null || lower is null)
                return;

            BeginEditUnit();
            lower.Strokes = lower.Strokes.AddRange(layer.Strokes);
            SetLayers(Normalize(document.Layers.Remove(layer)), lower);
            EndEditUnit();
        }

        bool CanDeleteLayer()
            => activeLayer is { } layer && document.Layers.Count > CountSubtree(layer);

        bool CanMoveLayer(int delta)
            => activeLayer is { } layer && FindSibling(document.Layers, layer, delta) is not null;

        static PenLayer? FindSibling(ImmutableList<PenLayer> layers, PenLayer layer, int delta)
        {
            var index = layers.IndexOf(layer);
            if (index < 0)
                return null;

            var step = delta > 0 ? 1 : -1;
            for (var i = index + step; i >= 0 && i < layers.Count; i += step)
            {
                if (layers[i].ParentId == layer.ParentId)
                    return layers[i];
            }
            return null;
        }

        void MoveLayer(int delta)
        {
            var layer = activeLayer;
            if (layer is null)
                return;

            var layers = document.Layers;
            var sibling = FindSibling(layers, layer, delta);
            if (sibling is null)
                return;

            var next = layers.Remove(layer);
            SetLayers(Normalize(next.Insert(next.IndexOf(sibling) + (delta > 0 ? 1 : 0), layer)), layer);
        }

        static PenLayer? FindLayer(ImmutableList<PenLayer> layers, Guid id)
        {
            foreach (var layer in layers)
            {
                if (layer.Id == id)
                    return layer;
            }
            return null;
        }

        static ImmutableList<PenLayer> Normalize(ImmutableList<PenLayer> layers)
        {
            var builder = ImmutableList.CreateBuilder<PenLayer>();
            var emitted = new HashSet<Guid>(layers.Count);
            EmitLayers(layers, Guid.Empty, 0, builder, emitted);

            foreach (var layer in layers)
            {
                if (!emitted.Add(layer.Id))
                    continue;
                layer.ParentId = Guid.Empty;
                layer.Depth = 0;
                builder.Add(layer);
            }
            return builder.ToImmutable();
        }

        static void EmitLayers(ImmutableList<PenLayer> layers, Guid parentId, int depth, ImmutableList<PenLayer>.Builder builder, HashSet<Guid> emitted)
        {
            foreach (var layer in layers)
            {
                if (layer.ParentId != parentId || !emitted.Add(layer.Id))
                    continue;

                layer.Depth = depth;
                if (layer.IsFolder)
                    EmitLayers(layers, layer.Id, depth + 1, builder, emitted);
                builder.Add(layer);
            }
        }

        static bool IsCollapsed(ImmutableList<PenLayer> layers, PenLayer layer)
        {
            var parentId = layer.ParentId;
            while (parentId != Guid.Empty)
            {
                var parent = FindLayer(layers, parentId);
                if (parent is null)
                    return false;
                if (!parent.IsExpanded)
                    return true;
                parentId = parent.ParentId;
            }
            return false;
        }

        void UpdateDisplayLayers(ImmutableList<PenLayer> layers)
        {
            var builder = ImmutableList.CreateBuilder<PenLayer>();
            for (var i = layers.Count - 1; i >= 0; i--)
            {
                if (!IsCollapsed(layers, layers[i]))
                    builder.Add(layers[i]);
            }
            DisplayLayers = builder.ToImmutable();
        }

        static void ExpandAncestors(ImmutableList<PenLayer> layers, PenLayer layer)
        {
            var parentId = layer.ParentId;
            while (parentId != Guid.Empty)
            {
                var parent = FindLayer(layers, parentId);
                if (parent is null)
                    return;

                parent.IsExpanded = true;
                parentId = parent.ParentId;
            }
        }

        void SetLayers(ImmutableList<PenLayer> layers, PenLayer? active)
        {
            document.Layers = layers;
            var next = active ?? (layers.IsEmpty ? null : layers[^1]);
            if (next is not null)
                ExpandAncestors(layers, next);
            UpdateDisplayLayers(layers);
            ActiveLayer = next;
            OnPropertyChanged(nameof(Layers));
            UpdateCommands();
            InvalidateDocument();
        }

        string CreateLayerName()
        {
            layerNumber++;
            return string.Format(Texts.LayerDefaultName, layerNumber);
        }

        string CreateFolderName()
        {
            folderNumber++;
            return string.Format(Texts.FolderDefaultName, folderNumber);
        }

        void UpdateCommands()
        {
            SelectAllCommand.RaiseCanExecuteChanged();
            DuplicateLayerCommand.RaiseCanExecuteChanged();
            DeleteLayerCommand.RaiseCanExecuteChanged();
            MergeLayerCommand.RaiseCanExecuteChanged();
            MoveLayerUpCommand.RaiseCanExecuteChanged();
            MoveLayerDownCommand.RaiseCanExecuteChanged();
            MoveIntoFolderCommand.RaiseCanExecuteChanged();
            MoveOutOfFolderCommand.RaiseCanExecuteChanged();
        }

        void OnDocumentChanged(object? sender, YukkuriMovieMaker.UndoRedo.UndoRedoEventArgs e)
        {
            if (isRestoring)
                return;

            isDirty = true;
            if (editDepth == 0)
                CommitSnapshot();
            OnPropertyChanged(nameof(IsLayerEditable));
            if (!IsLayerEditable)
                ClearSelection();
            UpdateSelectionCommands();
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
                if (isDisposed)
                    return;
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
            if (isDisposed)
                return;

            isDisposed = true;
            document.UndoRedoCommandCreated -= OnDocumentChanged;
            disposer.Dispose();
        }
    }
}

﻿using System.Linq;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Moe.Mottomo.ArcaeaSim.Subsystems.Rendering;
using Moe.Mottomo.ArcaeaSim.Subsystems.Scores.Entities;

namespace Moe.Mottomo.ArcaeaSim.Subsystems.Scores.Visualization {
    /// <inheritdoc cref="VisualNoteBase" />
    /// <inheritdoc cref="IRangedPreviewNote"/>
    /// <summary>
    /// Represents an arc visual note.
    /// </summary>
    public sealed class ArcVisualNote : VisualNoteBase, IRangedPreviewNote {

        public ArcVisualNote([NotNull] VisualBeatmap beatmap, [NotNull] ArcNote baseNote, [NotNull] StageMetrics metrics)
            : base(beatmap, baseNote) {
            _baseNote = baseNote;
            _metrics = metrics;

            PreviewStartY = beatmap.CalculateY(baseNote.StartTick, metrics, metrics.FinishLineY);
            PreviewEndY = beatmap.CalculateY(baseNote.EndTick, metrics, metrics.FinishLineY);

            _hexahedron = new ColoredHexahedron(beatmap.GraphicsDevice);

            if (baseNote.SkyNotes != null && baseNote.SkyNotes.Length > 0) {
                SkyNotes = baseNote.SkyNotes.Select(n => new SkyVisualNote(beatmap, n, metrics)).ToArray();
            }
        }

        public override void Draw(Effect effect, int beatmapTicks, float currentY) {
            var metrics = _metrics;
            var skyNotes = SkyNotes;
            var n = _baseNote;

            if (skyNotes?.Length > 0) {
                var skyNoteSize = new Vector3(metrics.SkyNoteWidth, metrics.SkyNoteHeight, metrics.SkyNoteTallness);

                foreach (var skyNote in skyNotes) {
                    if (!skyNote.IsVisible(beatmapTicks, currentY)) {
                        continue;
                    }

                    var ratio = (float)(skyNote.BaseNote.Tick - n.StartTick) / (n.EndTick - n.StartTick);

                    var xRatio = MathHelper.Lerp(n.StartX, n.EndX, ratio);
                    var yRatio = MathHelper.Lerp(n.StartY, n.EndY, ratio);

                    var left = (xRatio - -0.5f) * metrics.TrackInnerWidth / (1.5f - -0.5f) - metrics.HalfTrackInnerWidth - metrics.SkyNoteWidth / 2;
                    var bottom = skyNote.PreviewY - currentY;
                    var corner = metrics.SkyInputZ * yRatio - metrics.SkyNoteTallness / 2;

                    var bottomNearLeft = new Vector3(left, bottom, corner);

                    skyNote.SetVertices(bottomNearLeft, skyNoteSize);

                    skyNote.Draw(effect, beatmapTicks, currentY);
                }
            }

            float startX, startY, startZ;

            if (n.StartTick < beatmapTicks) {
                var ratio = (float)(beatmapTicks - n.StartTick) / (n.EndTick - n.StartTick);
                var xRatio = MathHelper.Lerp(n.StartX, n.EndX, ratio);
                var yRatio = MathHelper.Lerp(n.StartY, n.EndY, ratio);

                startX = (xRatio - -0.5f) * metrics.TrackInnerWidth / (1.5f - -0.5f) - metrics.HalfTrackInnerWidth;
                startY = metrics.FinishLineY;
                startZ = metrics.SkyInputZ * yRatio;
            } else {
                startX = (n.StartX - -0.5f) * metrics.TrackInnerWidth / (1.5f - -0.5f) - metrics.HalfTrackInnerWidth;
                startY = PreviewStartY - currentY;
                startZ = metrics.SkyInputZ * n.StartY;
            }

            var endX = (n.EndX - -0.5f) * metrics.TrackInnerWidth / (1.5f - -0.5f) - metrics.HalfTrackInnerWidth;
            var endY = PreviewEndY - currentY;
            var endZ = metrics.SkyInputZ * n.EndY;

            if (endY > metrics.TrackLength) {
                var ratio = (metrics.TrackLength - startY) / (endY - startY);

                endY = metrics.TrackLength;
                endX = MathHelper.Lerp(startX, endX, ratio);
                endZ = MathHelper.Lerp(startZ, endZ, ratio);
            }

            var start = new Vector3(startX, startY, startZ);
            var end = new Vector3(endX, endY, endZ);

            Color color;
            Vector2 arcSectionSize;

            if (n.IsPlayable) {
                color = _baseNote.Color == ArcColor.Magenta ? TranslucentRed : TranslucentRoyalBlue;
                arcSectionSize = new Vector2(metrics.PlayableArcWidth, metrics.PlayableArcTallness);
            } else {
                color = TranslucentMediumPurple;
                arcSectionSize = new Vector2(metrics.GuidingArcWidth, metrics.GuidingArcTallness);
            }

            DrawArc(effect.CurrentTechnique, start, end, _baseNote.Easing, color, arcSectionSize);

        }

        public override bool IsVisible(int beatmapTicks, float currentY) {
            if (_baseNote.EndTick < beatmapTicks - _metrics.PastTickThreshold || beatmapTicks + _metrics.FutureTickThreshold < _baseNote.StartTick) {
                return false;
            }

            return !(PreviewEndY < currentY || currentY + _metrics.TrackLength < PreviewStartY);
        }

        public float PreviewStartY { get; }

        public float PreviewEndY { get; }

        /// <summary>
        /// Gets the <see cref="SkyNote"/>s associated with this note.
        /// </summary>
        [CanBeNull]
        private SkyVisualNote[] SkyNotes { get; }

        protected override void Dispose(bool disposing) {
            if (SkyNotes != null) {
                foreach (var skyNote in SkyNotes) {
                    skyNote.Dispose();
                }
            }

            _hexahedron?.Dispose();
            _hexahedron = null;
        }

        private void DrawArc([NotNull] EffectTechnique technique, Vector3 start, Vector3 end, ArcEasing easing, Color color, Vector2 arcSectionSize) {
            const int segmentCount = 9;

            var lastPoint = start;

            for (var i = 1; i <= segmentCount; ++i) {
                Vector3 currentPoint;

                if (i == segmentCount) {
                    currentPoint = end;
                } else {
                    var ratio = (float)i / segmentCount;

                    currentPoint = ArcEasingHelper.Ease(start, end, ratio, easing);
                }

                _hexahedron.SetVertices(lastPoint, currentPoint, color, arcSectionSize);

                _hexahedron.Draw(technique);

                lastPoint = currentPoint;
            }
        }

        private static readonly Color TranslucentRed = new Color(Color.Red, 0.8f);
        private static readonly Color TranslucentRoyalBlue = new Color(Color.RoyalBlue, 0.8f);
        private static readonly Color TranslucentMediumPurple = new Color(Color.MediumPurple, 0.5f);

        private readonly ArcNote _baseNote;
        private readonly StageMetrics _metrics;
        private ColoredHexahedron _hexahedron;

    }
}
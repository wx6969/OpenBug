using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace OpenAnt
{
    public sealed class AntSurface : FrameworkElement
    {
        public IReadOnlyList<ProceduralAnt> Ants
        {
            get => (IReadOnlyList<ProceduralAnt>)(GetValue(AntsProperty) ?? Array.Empty<ProceduralAnt>());
            set => SetValue(AntsProperty, value);
        }

        public static readonly DependencyProperty AntsProperty =
            DependencyProperty.Register(
                nameof(Ants),
                typeof(IReadOnlyList<ProceduralAnt>),
                typeof(AntSurface),
                new FrameworkPropertyMetadata(Array.Empty<ProceduralAnt>(), FrameworkPropertyMetadataOptions.AffectsRender)
            );

        public AntSurface()
        {
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            var ants = Ants;
            for (int i = 0; i < ants.Count; i++)
            {
                ants[i].Draw(drawingContext);
            }
        }
    }
}

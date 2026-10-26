using App2d.Core.Imaging;

namespace App2d.ImageLab;

// Owned by the preview's single worker. Retains one source/channel and one sigma, not a cache per slider value.
internal sealed class RasterTransformProcessor
{
    private RasterFrame? _source;
    private ChannelSource _channelKind;
    private Image2D<float>? _channel;
    private float _sigma;
    private Image2D<float>? _strengths;
    private RasterRegions? _regions;
    private bool _eightConnected;

    public int ChannelConversionCount { get; private set; }
    public int CannyPreparationCount { get; private set; }
    public int RegionLabelingCount { get; private set; }
    public long CachedPixelBytes => ((long)(_channel?.Count ?? 0) + (_strengths?.Count ?? 0) + (_regions?.Labels.Count ?? 0)) * sizeof(float);

    // Use on the owning worker, or while it is idle. Committed stages are immutable; brush drafts never enter here.
    public void KeepSource(RasterFrame? source)
    {
        if (!ReferenceEquals(source, _source))
        {
            _source = source;
            _channel = null;
            _strengths = null;
            _regions = null;
        }
    }

    public RasterFrame Apply(RasterFrame source, RasterTransform transform)
    {
        KeepSource(source);
        if (transform.Kind is TransformKind.LargestRegion or TransformKind.RemoveSmallRegions && source.Mask is not null)
        {
            if (_regions is null || _eightConnected != transform.EightConnected)
            {
                _regions = new(source.Mask, transform.EightConnected);
                _eightConnected = transform.EightConnected;
                RegionLabelingCount++;
            }
            return transform.FilterRegions(_regions);
        }
        if (transform.Kind != TransformKind.Canny)
        {
            return transform.Apply(source);
        }
        // A scalar/mask source already determines its channel regardless of the dormant color-channel selector.
        var channelKind = source.Colors is null ? ChannelSource.Luminance : transform.Channel;
        if (_channel is null || _channelKind != channelKind)
        {
            _channel = source.GetChannel(channelKind);
            _channelKind = channelKind;
            _strengths = null;
            ChannelConversionCount++;
        }
        if (_strengths is null || _sigma != transform.Smoothing)
        {
            var strengths = _channel.CannyEdgeStrengths(transform.Smoothing);
            _strengths = strengths;
            _sigma = transform.Smoothing;
            CannyPreparationCount++;
        }
        return new(_strengths.HysteresisThreshold(transform.WeakEdge, transform.StrongEdge),
            "Thin edges · linked weak lines");
    }
}

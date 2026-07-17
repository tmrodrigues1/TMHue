using TMHue.Core.ValueObjects;

namespace TMHue.Core.Services;

/// <summary>Extracts the dominant colors of an image (or screen region) via median-cut
/// quantization. Runs entirely in-process over a pixel sample — no file ever leaves the
/// machine. Pure algorithm, deliberately free of any imaging/UI dependency: callers decode
/// their bitmap into an <see cref="RgbColor"/> array however they like.</summary>
public static class PaletteExtractor
{
    // Two candidate colors closer than this (redmean-weighted RGB distance) are treated as
    // shades of the same color and merged, freeing a palette slot for a genuinely different
    // color. ~40 keeps distinct hues apart while collapsing e.g. several near-blacks.
    private const double MergeDistance = 40.0;

    // How many median-cut boxes to produce per requested color before merging. Over-segmenting
    // lets a dominant color (a dark background, say) burn through its many boxes and still
    // leave boxes for minority colors; the merge pass then collapses its shades back into one.
    private const int OverSegmentationFactor = 4;

    /// <summary>Reduces <paramref name="pixels"/> to at most <paramref name="colorCount"/>
    /// representative colors, ordered from most to least dominant (by pixel population).
    /// Over-segments via median cut, then merges perceptually similar shades so one dominant
    /// color can't crowd distinct minority colors out of the palette.</summary>
    public static IReadOnlyList<RgbColor> Extract(IReadOnlyList<RgbColor> pixels, int colorCount = 5)
    {
        if (pixels.Count == 0) return Array.Empty<RgbColor>();

        var boxes = new List<List<RgbColor>> { new(pixels) };

        // Median cut: repeatedly split the box with the widest channel range at its median,
        // so each final box gathers pixels that are actually similar to each other.
        var targetBoxes = colorCount * OverSegmentationFactor;
        while (boxes.Count < targetBoxes)
        {
            var widest = FindWidestBox(boxes);
            if (widest is null) break; // every remaining box is a single flat color

            boxes.Remove(widest);
            var (first, second) = SplitAtMedian(widest);
            boxes.Add(first);
            boxes.Add(second);
        }

        // Merge pass: walk candidates from most to least populous; a candidate close to an
        // already-kept color just reinforces that color's population instead of taking a slot.
        var candidates = boxes
            .Select(box => (Color: Average(box), Count: (long)box.Count))
            .OrderByDescending(c => c.Count)
            .ToList();

        var kept = new List<(RgbColor Color, long Count)>();
        foreach (var candidate in candidates)
        {
            var mergedInto = -1;
            for (var i = 0; i < kept.Count; i++)
            {
                if (Distance(kept[i].Color, candidate.Color) < MergeDistance)
                {
                    mergedInto = i;
                    break;
                }
            }

            if (mergedInto >= 0)
                kept[mergedInto] = (kept[mergedInto].Color, kept[mergedInto].Count + candidate.Count);
            else
                kept.Add(candidate);
        }

        return kept
            .OrderByDescending(c => c.Count)
            .Take(colorCount)
            .Select(c => c.Color)
            .ToArray();
    }

    // "Redmean" perceptually-weighted RGB distance: cheap, no color-space conversion, yet far
    // closer to human perception than plain Euclidean RGB.
    private static double Distance(RgbColor a, RgbColor b)
    {
        var rMean = (a.Red + b.Red) / 2.0;
        double dr = a.Red - b.Red, dg = a.Green - b.Green, db = a.Blue - b.Blue;
        return Math.Sqrt(
            (2 + rMean / 256.0) * dr * dr +
            4 * dg * dg +
            (2 + (255 - rMean) / 256.0) * db * db);
    }

    private static List<RgbColor>? FindWidestBox(List<List<RgbColor>> boxes)
    {
        List<RgbColor>? widest = null;
        var widestRange = 0;
        foreach (var box in boxes)
        {
            if (box.Count < 2) continue;
            var range = ChannelRanges(box).Max;
            if (range > widestRange)
            {
                widestRange = range;
                widest = box;
            }
        }
        return widestRange > 0 ? widest : null;
    }

    private static (int Max, int Channel) ChannelRanges(List<RgbColor> box)
    {
        int minR = 255, maxR = 0, minG = 255, maxG = 0, minB = 255, maxB = 0;
        foreach (var c in box)
        {
            if (c.Red < minR) minR = c.Red;
            if (c.Red > maxR) maxR = c.Red;
            if (c.Green < minG) minG = c.Green;
            if (c.Green > maxG) maxG = c.Green;
            if (c.Blue < minB) minB = c.Blue;
            if (c.Blue > maxB) maxB = c.Blue;
        }

        var rangeR = maxR - minR;
        var rangeG = maxG - minG;
        var rangeB = maxB - minB;

        if (rangeG >= rangeR && rangeG >= rangeB) return (rangeG, 1);
        if (rangeR >= rangeB) return (rangeR, 0);
        return (rangeB, 2);
    }

    private static (List<RgbColor> First, List<RgbColor> Second) SplitAtMedian(List<RgbColor> box)
    {
        var channel = ChannelRanges(box).Channel;
        box.Sort((a, b) => ChannelValue(a, channel).CompareTo(ChannelValue(b, channel)));

        // Split on the value boundary nearest the population median, never through a run of
        // identical channel values: cutting mid-run would spread one flat color across both
        // halves and pull both averages toward a color that isn't actually in the image.
        var mid = box.Count / 2;
        var medianValue = ChannelValue(box[mid], channel);

        var runStart = mid;
        while (runStart > 0 && ChannelValue(box[runStart - 1], channel) == medianValue) runStart--;
        var runEnd = mid;
        while (runEnd < box.Count && ChannelValue(box[runEnd], channel) == medianValue) runEnd++;

        int split;
        if (runStart == 0) split = runEnd;
        else if (runEnd == box.Count) split = runStart;
        else split = mid - runStart <= runEnd - mid ? runStart : runEnd;

        return (box.GetRange(0, split), box.GetRange(split, box.Count - split));
    }

    private static byte ChannelValue(RgbColor c, int channel) => channel switch
    {
        0 => c.Red,
        1 => c.Green,
        _ => c.Blue
    };

    private static RgbColor Average(List<RgbColor> box)
    {
        long r = 0, g = 0, b = 0;
        foreach (var c in box)
        {
            r += c.Red;
            g += c.Green;
            b += c.Blue;
        }
        return new RgbColor(
            (byte)(r / box.Count),
            (byte)(g / box.Count),
            (byte)(b / box.Count));
    }
}

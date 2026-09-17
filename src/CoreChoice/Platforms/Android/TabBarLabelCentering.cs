using View = Android.Views.View;
using ViewGroup = Android.Views.ViewGroup;
using ViewTreeObserver = Android.Views.ViewTreeObserver;
using Android.Widget;
using Google.Android.Material.BottomNavigation;

namespace CoreChoice.Platforms.Android;

/// <summary>
/// Shell's bottom tab bar is a stock Material <see cref="BottomNavigationView"/> — native chrome
/// with no XAML surface, so it cannot be restyled from a page or a ResourceDictionary. Its item
/// layout reserves vertical space for an icon that none of the five tabs (Ask/Answers/Profile/
/// Coins/Settings) has, so the label sits low in the bar rather than centred — most visible now that the
/// bar is a fixed, correct 147px tall (see <c>MainActivity</c>'s inset listener; this class does not
/// touch that height, only where the label sits inside it).
///
/// Rather than guess at a fixed dp offset — which would only be right for the exact icon/padding
/// combination Material happens to ship today, and wrong the moment that changes — this measures
/// each item's actual label position after the bar first lays out and nudges
/// <see cref="Google.Android.Material.Navigation.NavigationBarView.ItemPaddingTop"/> /
/// <c>ItemPaddingBottom</c> by exactly the pixel delta needed to centre it, once, then leaves the
/// bar alone. Padding is additive screen-space offset, so this is exact regardless of whatever
/// icon-slot or margin internals produced the original gap.
/// </summary>
internal static class TabBarLabelCentering
{
    /// <summary>Watches <paramref name="root"/> (the same window content view MainActivity already
    /// pads for the bottom inset) for the bottom tab bar to appear, then centres its labels exactly
    /// once. Safe to call before Shell has built the bar — it just waits.</summary>
    public static void Apply(View root) =>
        root.ViewTreeObserver?.AddOnGlobalLayoutListener(new Listener(root));

    private sealed class Listener(View root) : Java.Lang.Object, ViewTreeObserver.IOnGlobalLayoutListener
    {
        private bool _done;

        public void OnGlobalLayout()
        {
            if (_done)
                return;

            if (Find<BottomNavigationView>(root) is not { Height: > 0 } bar)
                return;

            Center(bar);
            _done = true;
            root.ViewTreeObserver?.RemoveOnGlobalLayoutListener(this);
        }
    }

    private static void Center(BottomNavigationView bar)
    {
        if (bar.GetChildAt(0) is not ViewGroup itemsRow || itemsRow.ChildCount == 0)
            return;

        var deltas = new List<int>();
        for (var i = 0; i < itemsRow.ChildCount; i++)
        {
            if (itemsRow.GetChildAt(i) is not { } item || Find<TextView>(item) is not { } label)
                continue;

            var labelTop = TopRelativeTo(label, bar);
            var labelCenter = labelTop + label.Height / 2.0;
            deltas.Add((int)Math.Round(bar.Height / 2.0 - labelCenter));
        }

        if (deltas.Count == 0)
            return;

        // All items should agree; the middle value is robust to any one item's label not having
        // settled its final layout yet.
        deltas.Sort();
        var delta = deltas[deltas.Count / 2];
        if (delta == 0)
            return;

        bar.ItemPaddingTop = Math.Max(0, bar.ItemPaddingTop + delta);
        bar.ItemPaddingBottom = Math.Max(0, bar.ItemPaddingBottom - delta);
    }

    /// <summary>Sum of every ancestor's <see cref="View.Top"/> from <paramref name="view"/> up to
    /// (not including) <paramref name="ancestor"/> — <paramref name="view"/>'s own top edge, in
    /// <paramref name="ancestor"/>'s coordinate space.</summary>
    private static int TopRelativeTo(View view, View ancestor)
    {
        var top = 0;
        var current = view;
        while (!ReferenceEquals(current, ancestor) && current.Parent is View parent)
        {
            top += current.Top;
            current = parent;
        }
        return top;
    }

    private static T? Find<T>(View view) where T : View
    {
        if (view is T match)
            return match;

        if (view is ViewGroup group)
            for (var i = 0; i < group.ChildCount; i++)
                if (group.GetChildAt(i) is { } child && Find<T>(child) is { } found)
                    return found;

        return null;
    }
}

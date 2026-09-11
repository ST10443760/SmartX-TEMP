using SmartX.Shared.Models;

namespace SmartX.Shared.Validation;

/// <summary>
/// Recursively validates that a sensor's claimed deployment path exists as an
/// unbroken chain of nodes in the site's deployment tree, e.g. confirming
/// "Sub-Zone B" really sits under "Zone 1" which really sits under "Facility A".
/// Works for a tree of any depth - the recursion has nothing hard-coded about
/// how many levels a site happens to have.
/// </summary>
public static class DeploymentTreeValidator
{
    /// <summary>
    /// True if <paramref name="path"/> names an unbroken chain of nodes
    /// starting at <paramref name="root"/>.
    /// </summary>
    public static bool IsValidPath(DeploymentNode root, DeploymentPath path)
        => IsValidPath(root, path.Segments);

    public static bool IsValidPath(DeploymentNode root, IReadOnlyList<string> segments)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(segments);

        if (segments.Count == 0)
        {
            return true;
        }

        if (!string.Equals(root.Name, segments[0], StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Base case: this node matched the final remaining segment - the whole
        // path has been consumed, so the chain is valid regardless of whether
        // the node has further children.
        if (segments.Count == 1)
        {
            return true;
        }

        var remaining = segments.Skip(1).ToArray();
        foreach (var child in root.Children)
        {
            if (IsValidPath(child, remaining))
            {
                return true;
            }
        }

        // No child continued the chain - base case for an invalid, broken path.
        return false;
    }

    /// <summary>
    /// Same walk as <see cref="IsValidPath(DeploymentNode, DeploymentPath)"/> but
    /// returns the deepest node it managed to match, which the UI can use to
    /// tell the operator exactly where a registration's path broke.
    /// </summary>
    public static DeploymentNode? FindDeepestMatch(DeploymentNode root, IReadOnlyList<string> segments)
    {
        if (segments.Count == 0 || !string.Equals(root.Name, segments[0], StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (segments.Count == 1)
        {
            return root;
        }

        var remaining = segments.Skip(1).ToArray();
        foreach (var child in root.Children)
        {
            var match = FindDeepestMatch(child, remaining);
            if (match is not null)
            {
                return match;
            }
        }

        return root;
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public static class ObjectNames
{
    static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);
    static readonly Dictionary<string, string> parts = new Dictionary<string, string>(StringComparer.Ordinal);
    static readonly string[,] words =
    {
        { "advertisement", "ad" },
        { "Advertisement", "Ad" },
        { "Architecture", "Arch" },
        { "architecture", "arch" },
        { "Holographic", "Holo" },
        { "holographic", "holo" },
        { "Hologram", "Holo" },
        { "hologram", "holo" },
        { "projection", "proj" },
        { "Projection", "Proj" },
        { "Projected", "Proj" },
        { "projected", "proj" },
        { "Controller", "Ctrl" },
        { "controller", "ctrl" },
        { "Stabilized", "" },
        { "stabilized", "" },
        { "XRController", "XRCtrl" },
        { "Thumbstick", "Stick" },
        { "Pleasure", "" },
        { "pleasure", "" },
        { "subscription", "sub" },
        { "Subscription", "Sub" },
        { "dimensional", "dim" },
        { "Dimensional", "Dim" },
        { "Impossible", "Odd" },
        { "impossible", "odd" },
        { "Physical", "Real" },
        { "physical", "real" },
        { "Displaced", "Shift" },
        { "displaced", "shift" },
        { "Detached", "Loose" },
        { "detached", "loose" },
        { "Volumetric", "Volume" },
        { "volumetric", "volume" },
        { "Translucent", "Clear" },
        { "translucent", "clear" },
        { "Bioluminescent", "Glow" },
        { "bioluminescent", "glow" },
        { "Continuous", "Long" },
        { "continuous", "long" },
        { "navigation", "nav" },
        { "Navigation", "Nav" },
        { "Instruction", "Hint" },
        { "instruction", "hint" },
        { "pedestrian", "walk" },
        { "Pedestrian", "Walk" },
        { "monolith", "block" },
        { "Monolith", "Block" },
        { "luminaire", "lamp" },
        { "Luminaire", "Lamp" },
        { "beautification", "decor" },
        { "Beautification", "Decor" },
        { "Infrastructure", "Infra" },
        { "infrastructure", "infra" },
        { "Interaction", "Interact" },
        { "interaction", "interact" },
    };

    public static string Short(string value)
    {
        if (string.IsNullOrEmpty(value)) return "Object";
        switch (value)
        {
            case "Clay UI": return "ClayPanel";
            case "Reality Shift": return "ShiftStage";
        }
        if (Regex.IsMatch(value, "^[A-Za-z0-9]{1,24}$")) return value;
        if (cache.TryGetValue(value, out var cached)) return cached;
        var result = Normalize(value);
        if (result.Length == 0) result = "Object";
        cache[value] = result;
        return result;
    }

    static string Normalize(string value)
    {
        if (parts.TryGetValue(value, out var cached)) return cached;
        var result = value.Replace('_', ' ');
        for (int i = 0; i < words.GetLength(0); i++)
            result = Regex.Replace(result, @"\b" + words[i, 0] + @"\b", words[i, 1]);
        result = Regex.Replace(result, @"(?<=\d)\s+(?=-?\d)", "N");
        result = Regex.Replace(result, @"-(?=\d)", "Neg");
        result = result.Replace(":", "Col").Replace("/", "Part").Replace("+", "Plus").Replace(".", "Dot");
        result = Regex.Replace(result, "[^A-Za-z0-9]", "");
        parts[value] = result;
        return result;
    }

    public static bool Matches(string actual, string expected)
        => actual == expected || actual == Short(expected);

    public static bool StartsWith(string actual, string prefix, StringComparison comparison = StringComparison.Ordinal)
    {
        if (actual.StartsWith(prefix, comparison)) return true;
        var normalized = Normalize(prefix);
        return normalized.Length > 0 && actual.StartsWith(normalized, comparison);
    }

    public static bool Contains(string actual, string part)
    {
        if (actual.Contains(part)) return true;
        if (part == ":") return Regex.IsMatch(actual, @"\dCol(?:Neg)?\d");
        var normalized = Normalize(part);
        return normalized.Length > 0 && actual.Contains(normalized);
    }

    public static Transform Find(Transform parent, string path)
    {
        if (!parent) return null;
        var found = parent.Find(path);
        if (found) return found;
        var current = parent;
        foreach (var segment in path.Split('/'))
        {
            var next = current.Find(segment);
            if (!next) next = current.Find(Short(segment));
            if (!next) return null;
            current = next;
        }
        return current;
    }
}

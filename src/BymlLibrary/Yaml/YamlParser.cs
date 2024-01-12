﻿using BymlLibrary.Nodes.Containers;
using BymlLibrary.Nodes.Containers.HashMap;
using System.Buffers;
using System.Runtime.CompilerServices;
using YamlDotNet.Helpers;
using YamlDotNet.RepresentationModel;

namespace BymlLibrary.Yaml;

internal class YamlParser
{
    private static readonly SearchValues<char> _integer = SearchValues.Create("0123456789");
    private static readonly SearchValues<char> _decimal = SearchValues.Create(".0123456789");

    public static Byml Parse(string text)
    {
        StringReader reader = new(text);
        YamlStream yaml = [];
        yaml.Load(reader);

        if (yaml.Documents.FirstOrDefault() is YamlDocument document) {
            return Parse(document.RootNode);
        }

        throw new InvalidDataException("""
            No yaml documents could be found in the provided text
            """);
    }

    private static Byml Parse(YamlNode node)
    {
        return node.NodeType switch {
            YamlNodeType.Mapping => ParseMap((YamlMappingNode)node),
            YamlNodeType.Sequence => ParseSequence((YamlSequenceNode)node),
            YamlNodeType.Scalar => ParseScalar((YamlScalarNode)node),
            _ => throw new NotSupportedException($"""
                The YamlNodeType '{node.NodeType}' is not supported
                """)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Byml ParseScalar(YamlScalarNode scalar)
    {
        if (string.IsNullOrEmpty(scalar.Value)) {
            return new();
        }

        if (scalar.Tag.IsEmpty) {
            if (!scalar.Value.AsSpan().ContainsAnyExcept(_integer)) {
                return int.Parse(scalar.Value);
            }

            if (!scalar.Value.AsSpan().ContainsAnyExcept(_decimal)) {
                return float.Parse(scalar.Value);
            }

            bool isTrue = scalar.Value.Equals("true", StringComparison.CurrentCultureIgnoreCase);
            if (isTrue || scalar.Value.Equals("false", StringComparison.CurrentCultureIgnoreCase)) {
                return isTrue;
            }

            return scalar.Value;
        }

        return scalar.Tag.Value switch {
            "!u" or "!u32" => Convert.ToUInt32(scalar.Value[2..], 16),
            "!ul" or "!u64" => Convert.ToUInt64(scalar.Value[2..], 16),
            "!l" or "!s64" => long.Parse(scalar.Value),
            "!d" or "!f64" => double.Parse(scalar.Value),
            "!!binary" => Convert.FromBase64String(scalar.Value),
            _ => throw new NotSupportedException($"""
                Unsupported tag '{scalar.Tag.Value}'
                """)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Byml ParseSequence(YamlSequenceNode sequence)
    {
        BymlArray array = [];
        foreach (var node in sequence.Children) {
            array.Add(Parse(node));
        }

        return array;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Byml ParseMap(YamlMappingNode mapping)
    {
        if (mapping.Tag.IsEmpty) {
            BymlMap map = [];
            foreach ((var key, var node) in mapping.Children) {
                if (key is not YamlScalarNode scalar) {
                    throw new InvalidOperationException($"""
                        Could not parse key node of type '{key.NodeType}'
                        """);
                }

                if (string.IsNullOrEmpty(scalar.Value)) {
                    throw new NotSupportedException("""
                        Empty (null) keys are not supported
                        """);
                }

                map[scalar.Value] = Parse(node);
            }

            return map;
        }

        return mapping.Tag.Value switch {
            "h32" => ParseHashMap32(mapping.Children),
            "h64" => ParseHashMap64(mapping.Children),
            _ => throw new NotSupportedException($"""
                Unsupported mapping tag '{mapping.Tag.Value}'
                """)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Byml ParseHashMap32(in IOrderedDictionary<YamlNode, YamlNode> nodes)
    {
        BymlHashMap32 map = [];
        foreach ((var key, var node) in nodes) {
            if (key is not YamlScalarNode scalar) {
                throw new InvalidOperationException($"""
                    Could not parse key node of type '{key.NodeType}'
                    """);
            }

            if (string.IsNullOrEmpty(scalar.Value)) {
                throw new NotSupportedException("""
                    Empty (null) keys are not supported
                    """);
            }

            map[Convert.ToUInt32(scalar.Value[2..], 16)] = Parse(node);
        }

        return map;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static Byml ParseHashMap64(in IOrderedDictionary<YamlNode, YamlNode> nodes)
    {
        BymlHashMap64 map = [];
        foreach ((var key, var node) in nodes) {
            if (key is not YamlScalarNode scalar) {
                throw new InvalidOperationException($"""
                    Could not parse key node of type '{key.NodeType}'
                    """);
            }

            if (string.IsNullOrEmpty(scalar.Value)) {
                throw new NotSupportedException("""
                    Empty (null) keys are not supported
                    """);
            }

            map[Convert.ToUInt64(scalar.Value[2..], 16)] = Parse(node);
        }

        return map;
    }
}
// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

namespace Coherence.CodeGen
{
    using System.Collections.Generic;

    internal static class TemplateMethods
    {
        public static string GetSerializeMethod(string type)
        {
            switch (type)
            {
                case "System.Byte":
                    return "Byte";
                case "System.SByte":
                    return "SByte";
                case "System.Int16":
                    return "Short";
                case "System.UInt16":
                    return "UShort";
                case "System.Char":
                    return "Char";
                case "System.Int32":
                    return "IntegerRange";
                case "System.UInt32":
                    return "UIntegerRange";
                case "System.Int64":
                    return "Long";
                case "System.UInt64":
                    return "ULong";
                case "System.Double":
                    return "Double";
                case "System.Single":
                    return "Float";
                case "Vector2":
                case "System.Numerics.Vector2":
                    return "Vector2";
                case "Vector3":
                case "System.Numerics.Vector3":
                    return "Vector3";
                case "Color":
                case "System.Numerics.Vector4":
                    return "Color";
                case "Quaternion":
                case "System.Numerics.Quaternion":
                    return "Quaternion";
                case "System.String":
                    return "String";
                case "System.Boolean":
                    return "Bool";
                case "Transform":
                case "Coherence.Toolkit.CoherenceSync":
                case "GameObject":
                case "Entity":
                    return "Entity";
                case "System.Byte[]":
                    return "Bytes";
                case "Coherence.Connection.ClientID":
                    return "ClientID";
            }

            return string.Empty;
        }

        public static string GetDefaultSerializeParameters(string type, bool addComma)
        {
            var result = string.Empty;

            switch (type)
            {
                case "System.Int32":
                    result = "32, -2147483648";
                    break;
                case "System.UInt32":
                    result = "32, 0";
                    break;
                case "System.Single":
                case "Vector3":
                case "System.Numerics.Vector3":
                case "Vector2":
                case "System.Numerics.Vector2":
                case "Color":
                case "System.Numerics.Vector4":
                    result = "FloatMeta.NoCompression()";
                    break;
                case "Quaternion":
                case "System.Numerics.Quaternion":
                    result = "32";
                    break;
            }

            return !string.IsNullOrEmpty(result) ? $"{(addComma ? ", " : string.Empty)}{result}" : result;
        }

        public static string GetSerializeParametersFromOverrides(string type, Dictionary<string, string> overrides,
            bool addComma)
        {
            var result = string.Empty;

            switch (type)
            {
                case "System.Int32":
                    result =
                        $"{(overrides.TryGetValue("bits", out var over) ? over : 32)}, {(overrides.TryGetValue("range-min", out var min) ? min : -2147483648)}";
                    break;
                case "System.UInt32":
                    result =
                        $"{(overrides.TryGetValue("bits", out var overUint) ? overUint : 32)}, {(overrides.TryGetValue("range-min", out var minUi) ? minUi : 0)}";
                    break;
                case "System.Single":
                case "Vector3":
                case "System.Numerics.Vector3":
                case "Vector2":
                case "System.Numerics.Vector2":
                case "Color":
                case "System.Numerics.Vector4":

                    _ = overrides.TryGetValue("compression", out var compression);
                    string parameters;

                    if (string.IsNullOrEmpty(compression) || compression.Equals("None"))
                    {
                        parameters = "FloatMeta.NoCompression()";
                    }
                    else if (compression.Equals("FixedPoint"))
                    {
                        parameters =
                            $"FloatMeta.ForFixedPoint({overrides["range-min"]}, {overrides["range-max"]}, {overrides["precision"]}d)";
                    }
                    else
                    {
                        parameters = $"FloatMeta.ForTruncated({overrides["bits"]})";
                    }

                    result = $"{parameters}";
                    break;
                case "Quaternion":
                case "System.Numerics.Quaternion":
                    result = $"{overrides["bits"]}";
                    break;
            }

            return !string.IsNullOrEmpty(result) ? $"{(addComma ? ", " : string.Empty)}{result}" : result;
        }
    }
}

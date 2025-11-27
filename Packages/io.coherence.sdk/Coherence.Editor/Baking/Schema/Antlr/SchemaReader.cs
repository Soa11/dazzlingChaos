// Copyright (c) coherence ApS.
// See the license file in the package root for more information.

#if UNITY_5_3_OR_NEWER
// IMPORTANT: Used by the pure-dotnet client, DON'T REMOVE.
#define UNITY
#endif

namespace Coherence.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Tree;
    using Coherence.Toolkit;
    using Entities;
    using Utils;
    using Schema;

#if !UNITY
    using DictionaryOfStringString = Dictionary<string, string>;
#endif

    public static class SchemaReader
    {
        public static SchemaDefinition Read(string schemaText)
        {
            AntlrInputStream antlerStream = new AntlrInputStream(schemaText);
            var lexer = new SchemaLexer(antlerStream);
            CommonTokenStream tokenStream = new CommonTokenStream(lexer);
            SchemaParser parser = new SchemaParser(tokenStream);

            var listener = new SchemaListener();
            ParseTreeWalker.Default.Walk(listener, parser.schema());

            listener.schemaDefinition.SchemaId = HashCalc.SHA1Hash(schemaText);

            return listener.schemaDefinition;
        }
    }

    public class SchemaListener : SchemaBaseListener
    {
        public SchemaDefinition schemaDefinition = new();

        /// <summary>
        /// Removes the surrounding quotes from a quoted string.
        /// </summary>
        private static ReadOnlySpan<char> Sanitize(ReadOnlySpan<char> input) => input[1..^1];

        public override void EnterArchetype(SchemaParser.ArchetypeContext context)
        {
            var archetype = new ArchetypeDefinition(context.ident().GetText(), new List<ArchetypeLOD>());

            var id = 0;
            if (context.@override() != null)
            {
                foreach (var overrContext in context.@override().pair())
                {
                    if (overrContext.ident().GetText().Equals("id"))
                    {
                        id = int.Parse(Sanitize(overrContext.STRING().GetText()));
                    }
                }
            }

            archetype.id = id;
            ArchetypeLOD highestLod = null;

            foreach (var lodContext in context.lod())
            {
                var level = lodContext.INT().GetText();
                var distance = lodContext.lod_distance() != null
                    ? Sanitize(lodContext.lod_distance().STRING().GetText())
                    : "0";

                var lod = new ArchetypeLOD(int.Parse(level), float.Parse(distance));

                foreach (var componentContext in lodContext.component_override())
                {
                    var item = new ArchetypeItem(componentContext.ident().GetText(),
                        new List<ArchetypeItemField>());

                    var componentOverrideId = 0;
                    var baseComponentId = 0;

                    if (componentContext.@override() != null)
                    {
                        foreach (var overrContext in componentContext.@override().pair())
                        {
                            if (overrContext.ident().GetText().Equals("id"))
                            {
                                componentOverrideId = int.Parse(Sanitize(overrContext.STRING().GetText()));
                            }
                            else if (overrContext.ident().GetText().Equals("base-id"))
                            {
                                baseComponentId = int.Parse(Sanitize(overrContext.STRING().GetText()));
                            }
                        }
                    }

                    item.id = componentOverrideId;
                    item.baseComponentId = baseComponentId;

                    foreach (var fieldContext in componentContext.field_override())
                    {
                        var overrides = new DictionaryOfStringString();

                        foreach (var overrContext in fieldContext.@override().pair())
                        {
                            overrides.Add(overrContext.ident().GetText(), Sanitize(overrContext.STRING().GetText()).ToString());
                        }

                        var field = new ArchetypeItemField(fieldContext.ident().GetText(), overrides);
                        item.fields.Add(field);
                    }

                    lod.items.Add(item);
                }

                FindExcludedComponents(lod, ref highestLod);

                archetype.lods.Add(lod);
            }

            schemaDefinition.ArchetypeDefinitions.Add(archetype);

            base.EnterArchetype(context);
        }

        private void FindExcludedComponents(ArchetypeLOD lod, ref ArchetypeLOD highestLod)
        {
            if (lod.level == 0)
            {
                highestLod = lod;
            }
            else
            {
                foreach (var component in highestLod.items)
                {
                    bool foundInCurrentLod = lod.items.Any(componentInCurrent =>
                        component.componentName.Equals(componentInCurrent.componentName));

                    if (!foundInCurrentLod)
                    {
                        lod.excludedComponentNames.Add(component.componentName);
                    }
                }
            }
        }

        public override void EnterComponent(SchemaParser.ComponentContext context)
        {
            var component = new ComponentDefinition(context.ident().GetText());
            var componentOverrides = new DictionaryOfStringString();

            var id = 0;
            if (context.@override() != null)
            {
                foreach (var overrContext in context.@override().pair())
                {
                    if (overrContext.ident().GetText().Equals("id"))
                    {
                        id = int.Parse(Sanitize(overrContext.STRING().GetText()));
                    }
                    else
                    {
                        componentOverrides.Add(overrContext.ident().GetText(), Sanitize(overrContext.STRING().GetText()).ToString());
                    }
                }
            }

            component.id = id;
            component.overrides = componentOverrides;

            var i = 0;
            var fieldOffset = 0;
            foreach (var field in context.field())
            {
                var bitMask = 1 << i;
                var name = field.ident().GetText();
                var type = field.field_type().GetText();
                // If we don't know the type, fallback to Int32. This is the case for enums
                var cSharpType = TypeUtils.GetCSharpTypeForSchemaType(type) ?? typeof(Int32);
                var schemaType = TypeUtils.GetSchemaType(cSharpType);
                var offsetSize = TypeUtils.GetFieldOffsetForSchemaType(schemaType);
                var overrides = new DictionaryOfStringString();

                if (field.@override() != null)
                {
                    foreach (var overr in field.@override().pair())
                    {
                        overrides.Add(overr.ident().GetText(), Sanitize(overr.STRING().GetText()).ToString());
                    }
                }

                var member = new ComponentMemberDescription(
                    NameMangler.MangleSchemaIdentifier(name),
                    NameMangler.MangleCSharpIdentifier(name),
                    type,
                    cSharpType == typeof(Entity) || cSharpType.FullName.Contains("UnityEngine")
                        ? cSharpType.Name
                        : cSharpType.FullName, TypeUtils.GetStringifiedBitMask(bitMask), fieldOffset, schemaType == SchemaType.Enum, overrides);
                component.members.Add(member);

                i++;
                fieldOffset += offsetSize;
            }

            component.bitMasks = TypeUtils.GetStringifiedBitMask((1 << component.members.Count) - 1);
            component.totalSize = fieldOffset;

            schemaDefinition.ComponentDefinitions.Add(component);
            base.EnterComponent(context);
        }

        public override void EnterCommand(SchemaParser.CommandContext context)
        {
            var command = new CommandDefinition(context.ident().GetText());

            var routing = "All";
            var id = 0;
            if (context.@override() != null)
            {
                foreach (var overrContext in context.@override().pair())
                {
                    if (overrContext.ident().GetText().Equals("routing"))
                    {
                        routing = Sanitize(overrContext.STRING().GetText()).ToString();
                    }

                    if (overrContext.ident().GetText().Equals("id"))
                    {
                        id = int.Parse(Sanitize(overrContext.STRING().GetText()));
                    }
                }
            }

            command.id = id;
            command.routing = Enum.Parse<MessageTarget>(routing);

            int fieldOffset = 0;
            foreach (var fieldContext in context.field())
            {
                var name = fieldContext.ident().GetText();
                var type = fieldContext.field_type().GetText();
                // If we don't know the type, fallback to Int32. This is the case for enums
                var cSharpType = TypeUtils.GetCSharpTypeForSchemaType(type) ?? typeof(Int32);
                var schemaType = TypeUtils.GetSchemaType(cSharpType);
                var fieldSize = TypeUtils.GetFieldOffsetForSchemaType(schemaType);
                var overrides = new DictionaryOfStringString();

                if (fieldContext.@override() != null)
                {
                    foreach (var overr in fieldContext.@override().pair())
                    {
                        overrides.Add(overr.ident().GetText(), Sanitize(overr.STRING().GetText()).ToString());
                    }
                }

                var member = new ComponentMemberDescription(
                    NameMangler.MangleSchemaIdentifier(name),
                    NameMangler.MangleCSharpIdentifier(name),
                    type,
                    cSharpType == typeof(Entity) || cSharpType.FullName.Contains("UnityEngine")
                        ? cSharpType.Name
                        : cSharpType.FullName, string.Empty, fieldOffset, schemaType == SchemaType.Enum, overrides);
                command.members.Add(member);

                fieldOffset += fieldSize;
            }

            command.totalSize = fieldOffset;
            schemaDefinition.CommandDefinitions.Add(command);

            base.EnterCommand(context);
        }

        public override void EnterInput(SchemaParser.InputContext context)
        {
            InputDefinition input = new InputDefinition(context.ident().GetText(), new List<ComponentMemberDescription>(), 0);

            int id = 0;
            if (context.@override() != null)
            {
                foreach (var overrContext in context.@override().pair())
                {
                    if (overrContext.ident().GetText().Equals("id"))
                    {
                        id = int.Parse(Sanitize(overrContext.STRING().GetText()));
                    }
                }
            }

            input.id = id;

            int fieldOffset = 0;
            foreach (var fieldContext in context.field())
            {
                var name = fieldContext.ident().GetText();
                var type = fieldContext.field_type().GetText();
                // If we don't know the type, fallback to Int32. This is the case for enums
                var cSharpType = TypeUtils.GetCSharpTypeForSchemaType(type) ?? typeof(Int32);
                var schemaType = Enum.Parse<SchemaType>(type);
                var fieldSize = TypeUtils.GetFieldOffsetForSchemaType(schemaType);
                var overrides = new DictionaryOfStringString();

                if (fieldContext.@override() != null)
                {
                    foreach (var overr in fieldContext.@override().pair())
                    {
                        overrides.Add(overr.ident().GetText(), Sanitize(overr.STRING().GetText()).ToString());
                    }
                }

                var member = new ComponentMemberDescription(
                    NameMangler.MangleSchemaIdentifier(name),
                    NameMangler.MangleCSharpIdentifier(name),
                    type,
                    cSharpType == typeof(Entity) || cSharpType.FullName.Contains("UnityEngine")
                        ? cSharpType.Name
                        : cSharpType.FullName, string.Empty, fieldOffset, schemaType == SchemaType.Enum, overrides);
                input.members.Add(member);

                fieldOffset += fieldSize;
            }

            input.totalSize = fieldOffset;
            schemaDefinition.InputDefinitions.Add(input);

            base.EnterInput(context);
        }
    }
}

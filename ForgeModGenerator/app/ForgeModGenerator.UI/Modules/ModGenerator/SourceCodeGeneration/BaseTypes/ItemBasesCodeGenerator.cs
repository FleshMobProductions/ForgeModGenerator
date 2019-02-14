﻿using ForgeModGenerator.CodeGeneration;
using ForgeModGenerator.CodeGeneration.JavaCodeDom;
using ForgeModGenerator.ModGenerator.Models;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;

namespace ForgeModGenerator.ModGenerator.SourceCodeGeneration
{
    public class ItemBasesCodeGenerator : MultiScriptsCodeGenerator
    {
        public ItemBasesCodeGenerator(Mod mod) : base(mod)
        {
            string folder = Path.Combine(ModPaths.GeneratedSourceCodeFolder(Modname, Organization), "item");
            ScriptFilePaths = new string[] {
                Path.Combine(folder, "ItemBase.java"),
                Path.Combine(folder, "bow", "BowBase.java"),
                Path.Combine(folder, "food", "FoodBase.java"),
                Path.Combine(folder, "armor", "ArmorBase.java"),
                Path.Combine(folder, "tool", "SwordBase.java"),
                Path.Combine(folder, "tool", "SpadeBase.java"),
                Path.Combine(folder, "tool", "PickaxeBase.java"),
                Path.Combine(folder, "tool", "HoeBase.java"),
                Path.Combine(folder, "tool", "AxeBase.java"),
            };
        }

        protected override string[] ScriptFilePaths { get; }

        protected override CodeCompileUnit CreateTargetCodeUnit(string scriptPath)
        {
            Parameter[] parameters = null;
            CodeCompileUnit unit = null;
            string fileName = Path.GetFileNameWithoutExtension(scriptPath);
            switch (fileName)
            {
                case "ItemBase":
                    return CreateBaseItemUnit(fileName, "Item", false);
                case "BowBase":
                    return CreateBaseItemUnit(fileName, "ItemBow", false);
                case "SwordBase":
                    return CreateBaseItemUnit(fileName, "ItemSword", true);
                case "SpadeBase":
                    return CreateBaseItemUnit(fileName, "ItemSpade", true);
                case "PickaxeBase":
                    return CreateBaseItemUnit(fileName, "ItemPickaxe", true);
                case "HoeBase":
                    return CreateBaseItemUnit(fileName, "ItemHoe", true);

                case "AxeBase":
                    unit = CreateBaseItemUnit(fileName, "ItemAxe", true);
                    CodeConstructor ctor = (CodeConstructor)unit.Namespaces[0].Types[0].Members[0];
                    CodeSuperConstructorInvokeExpression super = (CodeSuperConstructorInvokeExpression)((CodeExpressionStatement)ctor.Statements[0]).Expression;
                    super.AddParameter(6.0F);
                    super.AddParameter(-3.2F);
                    return unit;

                case "FoodBase":
                    parameters = new Parameter[] {
                        new Parameter(typeof(string).FullName, "name"),
                        new Parameter(typeof(int).FullName, "amount"),
                        new Parameter(typeof(float).FullName, "saturation"),
                        new Parameter(typeof(bool).FullName, "isAnimalFood")
                    };
                    return CreateCustomItemUnit(fileName, "ItemFood", parameters);

                case "ArmorBase":
                    parameters = new Parameter[] {
                        new Parameter(typeof(string).FullName, "name"),
                        new Parameter("ArmorMaterial", "materialIn"),
                        new Parameter(typeof(int).FullName, "renderIndexIn"),
                        new Parameter("EntityEquipmentSlot", "equipmentSlotIn")
                    };
                    unit = CreateCustomItemUnit(fileName, "ItemArmor", parameters);
                    unit.Namespaces[0].Imports.Add(new CodeNamespaceImport("net.minecraft.inventory.EntityEquipmentSlot"));
                    return unit;
                default:
                    throw new NotImplementedException($"CodeCompileUnit for {fileName} not found");
            }
        }

        private CodeCompileUnit CreateBaseItemUnit(string className, string baseType, bool tool = false)
        {
            Parameter[] toolParameters = null;
            if (tool)
            {
                toolParameters = new Parameter[] {
                    new Parameter(typeof(string).FullName, "name"),
                    new Parameter("ToolMaterial", "material")
                };
            }
            else
            {
                toolParameters = new Parameter[] {
                    new Parameter(typeof(string).FullName, "name")
                };
            }
            return CreateCustomItemUnit(className, baseType, toolParameters);
        }

        private CodeCompileUnit CreateCustomItemUnit(string className, string baseType, params Parameter[] ctorParameters)
        {
            CodeTypeDeclaration clas = CreateBaseItemClass(className, baseType, ctorParameters);
            return NewCodeUnit(clas, $"{GeneratedPackageName}.{Modname}",
                                     $"{GeneratedPackageName}.gui.{Modname}CreativeTab",
                                     $"{GeneratedPackageName}.{Modname}Items",
                                     $"{GeneratedPackageName}.handler.IHasModel",
                                     $"net.minecraft.item.{baseType}");
        }

        private CodeMemberMethod CreateItemRegisterModelsMethod()
        {
            // TODO: Add annotation @Override
            CodeMemberMethod method = NewMethod("registerModels", typeof(void).FullName, MemberAttributes.Public);
            CodeMethodInvokeExpression getProxy = NewMethodInvokeVar(Modname, "getProxy");
            method.Statements.Add(NewMethodInvoke(getProxy, "registerItemRenderer", NewVarReference("this"), NewPrimitive(0), NewPrimitive("inventory")));
            return method;
        }

        private CodeTypeDeclaration CreateBaseItemClass(string className, string baseClass, params Parameter[] ctorParameters)
        {
            CodeConstructor ctor = NewConstructor(className, MemberAttributes.Public);
            string[] superParameters = null;
            if (ctorParameters != null && ctorParameters.Length > 0)
            {
                List<string> parameterNames = new List<string>(ctorParameters.Length);
                ctor.Parameters.Add(NewParameter(ctorParameters[0].TypeName, ctorParameters[0].Name)); // do not add first param to super arguments
                for (int i = 1; i < ctorParameters.Length; i++)
                {
                    ctor.Parameters.Add(NewParameter(ctorParameters[i].TypeName, ctorParameters[i].Name));
                    parameterNames.Add(ctorParameters[i].Name);
                }
                superParameters = parameterNames.ToArray();
            }
            foreach (CodeExpression item in GetCtorInitializators(superParameters))
            {
                ctor.Statements.Add(item);
            }
            return CreateBaseItemClass(className, baseClass, ctor);
        }

        private CodeTypeDeclaration CreateBaseItemClass(string className, string baseClass, CodeConstructor ctor)
        {
            CodeTypeDeclaration clas = NewClassWithBases(className, false, baseClass, "IHasModel");
            clas.Members.Add(ctor);
            clas.Members.Add(CreateItemRegisterModelsMethod());
            return clas;
        }

        private CodeExpression[] GetCtorInitializators(params string[] superParameters)
        {
            CodeExpression[] ctorArgs = null;
            if (superParameters != null && superParameters.Length > 0)
            {
                List<CodeExpression> ctorArgsList = new List<CodeExpression>(superParameters.Length);
                foreach (string param in superParameters)
                {
                    ctorArgsList.Add(NewVarReference(param));
                }
                ctorArgs = ctorArgsList.ToArray();
            }
            CodeSuperConstructorInvokeExpression super = new CodeSuperConstructorInvokeExpression(ctorArgs);
            CodeMethodInvokeExpression setUnlocalizedName = NewMethodInvoke("setUnlocalizedName", NewVarReference("name"));
            CodeMethodInvokeExpression setRegistryName = NewMethodInvoke("setRegistryName", NewVarReference("name"));
            CodeMethodInvokeExpression setCreativeTab = NewMethodInvoke("setCreativeTab", NewVarReference(Modname + "CreativeTab.MODCEATIVETAB"));
            CodeMethodInvokeExpression addToList = NewMethodInvoke(NewFieldReferenceVar(Modname + "Items", "ITEMS"), "add", NewThis());
            return new CodeExpression[] { super, setUnlocalizedName, setRegistryName, setCreativeTab, addToList };
        }
    }
}
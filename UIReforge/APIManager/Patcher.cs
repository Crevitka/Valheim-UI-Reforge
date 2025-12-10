using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Preloader;
using BepInEx.Preloader.Patching;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;

namespace APIManager;

internal static class Patcher
{
	private class MonoAssemblyResolver : IAssemblyResolver, IDisposable
	{
		public AssemblyDefinition Resolve(AssemblyNameReference name)
		{
			return AssemblyDefinition.ReadAssembly(AppDomain.CurrentDomain.Load(name.FullName).Location);
		}

		public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
		{
			return Resolve(name);
		}

		public void Dispose()
		{
		}
	}

	private class AssemblyLoadInterceptor
	{
		private static string? assemblyPath;

		private static MethodInfo TargetMethod()
		{
			return AccessTools.DeclaredMethod(typeof(Assembly), "Load", new Type[1] { typeof(byte[]) });
		}

		private static bool Prefix(ref byte[] __0, ref Assembly? __result)
		{
			assemblyPath = null;
			if (modifyNextLoad)
			{
				modifyNextLoad = false;
				try
				{
					using AssemblyDefinition assemblyDefinition = AssemblyDefinition.ReadAssembly(new MemoryStream(__0), new ReaderParameters
					{
						AssemblyResolver = new MonoAssemblyResolver()
					});
					((Dictionary<string, string>)typeof(EnvVars).Assembly.GetType("BepInEx.Preloader.RuntimeFixes.UnityPatches").GetProperty("AssemblyLocations").GetValue(null))[assemblyDefinition.FullName] = currentAssemblyPath;
					FixupModuleReferences(assemblyDefinition.MainModule);
					using MemoryStream memoryStream = new MemoryStream();
					assemblyDefinition.Write(memoryStream);
					__0 = memoryStream.ToArray();
					string dumpedAssembliesPath = Patcher.dumpedAssembliesPath;
					char directorySeparatorChar = Path.DirectorySeparatorChar;
					string path = dumpedAssembliesPath + directorySeparatorChar + assemblyDefinition.Name.Name + ".dll";
					if (loadDumpedAssemblies.Value || dumpAssemblies.Value)
					{
						File.WriteAllBytes(path, __0);
					}
					if (loadDumpedAssemblies.Value)
					{
						assemblyPath = path;
						__result = null;
						return false;
					}
				}
				catch (BadImageFormatException)
				{
				}
				catch (Exception ex2)
				{
					Debug.LogError("Failed patching ... " + ex2);
				}
			}
			return true;
		}

		private static void Postfix(ref Assembly? __result)
		{
			if (assemblyPath != null && __result == null)
			{
				__result = Assembly.LoadFrom(assemblyPath);
				assemblyPath = null;
			}
		}
	}

	private static PluginInfo? lastPluginInfo;

	private static bool modifyNextLoad = false;

	private static string modGUID = null;

	private static HashSet<string> redirectedNamespaces = null;

	private static readonly Assembly patchingAssembly = Assembly.GetExecutingAssembly();

	private static string currentAssemblyPath = null;

	private static readonly ConfigEntry<bool> dumpAssemblies = (ConfigEntry<bool>)AccessTools.DeclaredField(typeof(AssemblyPatcher), "ConfigDumpAssemblies").GetValue(null);

	private static readonly ConfigEntry<bool> loadDumpedAssemblies = (ConfigEntry<bool>)AccessTools.DeclaredField(typeof(AssemblyPatcher), "ConfigLoadDumpedAssemblies").GetValue(null);

	private static readonly string dumpedAssembliesPath = (string)AccessTools.DeclaredField(typeof(AssemblyPatcher), "DumpedAssembliesPath").GetValue(null);

	private static void GrabPluginInfo(PluginInfo __instance)
	{
		lastPluginInfo = __instance;
	}

	[HarmonyPriority(700)]
	private static void CheckAssemblyLoadFile(string __0)
	{
		if (__0 == lastPluginInfo?.Location && lastPluginInfo.Dependencies.Any((BepInDependency d) => d.DependencyGUID == modGUID))
		{
			modifyNextLoad = true;
			currentAssemblyPath = __0;
		}
		lastPluginInfo = null;
	}

	[HarmonyPriority(500)]
	private static bool InterceptAssemblyLoadFile(string __0, ref Assembly? __result)
	{
		if (modifyNextLoad && (object)__result == null)
		{
			__result = Assembly.Load(File.ReadAllBytes(__0));
			return false;
		}
		return true;
	}

	private static void FixupModuleReferences(ModuleDefinition module)
	{
		ModuleDefinition module2 = module;
		foreach (TypeDefinition type5 in module2.GetTypes())
		{
			if ((object)patchingAssembly.GetType(type5.FullName) == null)
			{
				Dispatch(type5);
			}
		}
		static bool AreSame(TypeReference a, TypeReference b)
		{
			return (bool)typeof(MetadataResolver).Assembly.GetType("Mono.Cecil.MetadataResolver").GetMethod("AreSame", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[2]
			{
				typeof(TypeReference),
				typeof(TypeReference)
			}, null).Invoke(null, new object[2] { a, b });
		}
		static TypeReference baseDeclaringType(TypeReference type)
		{
			while (type.DeclaringType != null)
			{
				type = type.DeclaringType;
			}
			return type;
		}
		void Dispatch(TypeDefinition type)
		{
			if (type.BaseType != null && type.BaseType.Scope == module2 && redirectedNamespaces.Contains(baseDeclaringType(type.BaseType).Namespace))
			{
				Type type4 = patchingAssembly.GetType(type.BaseType.FullName);
				if ((object)type4 != null)
				{
					type.BaseType = module2.ImportReference(type4);
				}
			}
			DispatchGenericParameters(type, type.FullName);
			DispatchInterfaces(type, type.FullName);
			DispatchAttributes(type, type.FullName);
			DispatchFields(type, type.FullName);
			DispatchProperties(type, type.FullName);
			DispatchEvents(type, type.FullName);
			DispatchMethods(type);
		}
		void DispatchAttributes(Mono.Cecil.ICustomAttributeProvider provider, string referencingEntityName)
		{
			if (!provider.HasCustomAttributes)
			{
				return;
			}
			foreach (CustomAttribute customAttribute in provider.CustomAttributes)
			{
				MethodReference methodReference4 = importMethodReference(customAttribute.Constructor);
				if (methodReference4 != null)
				{
					customAttribute.Constructor = methodReference4;
				}
				else
				{
					VisitMethod(customAttribute.Constructor, referencingEntityName);
				}
				for (int n = 0; n < customAttribute.ConstructorArguments.Count; n++)
				{
					CustomAttributeArgument customAttributeArgument = customAttribute.ConstructorArguments[n];
					customAttribute.ConstructorArguments[n] = new CustomAttributeArgument(VisitType(customAttributeArgument.Type, referencingEntityName), customAttributeArgument.Value);
				}
				for (int num = 0; num < customAttribute.Properties.Count; num++)
				{
					Mono.Cecil.CustomAttributeNamedArgument customAttributeNamedArgument = customAttribute.Properties[num];
					customAttribute.Properties[num] = new Mono.Cecil.CustomAttributeNamedArgument(customAttributeNamedArgument.Name, new CustomAttributeArgument(VisitType(customAttributeNamedArgument.Argument.Type, referencingEntityName), customAttributeNamedArgument.Argument.Value));
				}
				for (int num2 = 0; num2 < customAttribute.Fields.Count; num2++)
				{
					Mono.Cecil.CustomAttributeNamedArgument customAttributeNamedArgument2 = customAttribute.Fields[num2];
					customAttribute.Fields[num2] = new Mono.Cecil.CustomAttributeNamedArgument(customAttributeNamedArgument2.Name, new CustomAttributeArgument(VisitType(customAttributeNamedArgument2.Argument.Type, referencingEntityName), customAttributeNamedArgument2.Argument.Value));
				}
			}
		}
		void DispatchEvents(TypeDefinition type, string referencingEntityName)
		{
			foreach (EventDefinition @event in type.Events)
			{
				@event.EventType = VisitType(@event.EventType, referencingEntityName);
				DispatchAttributes(@event, referencingEntityName);
			}
		}
		void DispatchFields(TypeDefinition type, string referencingEntityName)
		{
			foreach (FieldDefinition field3 in type.Fields)
			{
				field3.FieldType = VisitType(field3.FieldType, referencingEntityName);
				DispatchAttributes(field3, referencingEntityName);
			}
		}
		void DispatchGenericArguments(IGenericInstance genericInstance, string referencingEntityName)
		{
			for (int j = 0; j < genericInstance.GenericArguments.Count; j++)
			{
				genericInstance.GenericArguments[j] = VisitType(genericInstance.GenericArguments[j], referencingEntityName);
			}
		}
		void DispatchGenericParameters(IGenericParameterProvider provider, string referencingEntityName)
		{
			foreach (GenericParameter genericParameter in provider.GenericParameters)
			{
				DispatchAttributes(genericParameter, referencingEntityName);
				for (int num3 = 0; num3 < genericParameter.Constraints.Count; num3++)
				{
					genericParameter.Constraints[num3] = VisitType(genericParameter.Constraints[num3], referencingEntityName);
				}
			}
		}
		void DispatchInterfaces(TypeDefinition type, string referencingEntityName)
		{
			foreach (InterfaceImplementation @interface in type.Interfaces)
			{
				@interface.InterfaceType = VisitType(@interface.InterfaceType, referencingEntityName);
			}
		}
		void DispatchMethod(MethodDefinition method)
		{
			method.ReturnType = VisitType(method.ReturnType, method.FullName);
			DispatchAttributes(method.MethodReturnType, method.FullName);
			DispatchGenericParameters(method, method.FullName);
			foreach (ParameterDefinition parameter in method.Parameters)
			{
				parameter.ParameterType = VisitType(parameter.ParameterType, method.FullName);
				DispatchAttributes(parameter, method.FullName);
			}
			for (int i = 0; i < method.Overrides.Count; i++)
			{
				MethodReference methodReference2 = importMethodReference(method.Overrides[i]);
				if (methodReference2 != null)
				{
					method.Overrides[i] = methodReference2;
				}
				else
				{
					VisitMethod(method.Overrides[i], method.FullName);
				}
			}
			if (method.HasBody)
			{
				DispatchMethodBody(method.Body);
			}
		}
		void DispatchMethodBody(Mono.Cecil.Cil.MethodBody body)
		{
			foreach (VariableDefinition variable in body.Variables)
			{
				variable.VariableType = VisitType(variable.VariableType, body.Method.FullName);
			}
			foreach (Instruction instruction in body.Instructions)
			{
				object operand = instruction.Operand;
				object obj = operand;
				if (!(obj is FieldReference field2))
				{
					if (!(obj is MethodReference method2))
					{
						if (obj is TypeReference type2)
						{
							instruction.Operand = VisitType(type2, body.Method.FullName);
						}
					}
					else
					{
						MethodReference methodReference = importMethodReference(method2);
						if (methodReference != null)
						{
							instruction.Operand = methodReference;
						}
						else
						{
							VisitMethod(method2, body.Method.FullName);
						}
					}
				}
				else
				{
					FieldReference fieldReference = importFieldReference(field2);
					if (fieldReference != null)
					{
						instruction.Operand = fieldReference;
					}
					else
					{
						VisitField(field2, body.Method.FullName);
					}
				}
			}
		}
		void DispatchMethods(TypeDefinition type)
		{
			foreach (MethodDefinition method4 in type.Methods)
			{
				DispatchMethod(method4);
			}
		}
		void DispatchProperties(TypeDefinition type, string referencingEntityName)
		{
			foreach (PropertyDefinition property in type.Properties)
			{
				property.PropertyType = VisitType(property.PropertyType, referencingEntityName);
				DispatchAttributes(property, referencingEntityName);
			}
		}
		TypeReference FixupType(TypeReference type)
		{
			if (type.Scope == module2 && redirectedNamespaces.Contains(baseDeclaringType(type).Namespace))
			{
				if (type.IsNested)
				{
					return FixupType(type.DeclaringType);
				}
				Type type3 = patchingAssembly.GetType(type.FullName);
				if ((object)type3 != null)
				{
					return module2.ImportReference(type3);
				}
			}
			return type;
		}
		FieldReference? importFieldReference(FieldReference field)
		{
			if (field.DeclaringType.Scope == module2 && redirectedNamespaces.Contains(baseDeclaringType(field.DeclaringType).Namespace))
			{
				FieldInfo fieldInfo = patchingAssembly.GetType(field.DeclaringType.FullName)?.GetField(field.Name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if ((object)fieldInfo != null)
				{
					return module2.ImportReference(fieldInfo);
				}
			}
			return null;
		}
		MethodReference? importMethodReference(MethodReference method)
		{
			MethodReference method3 = method;
			if (method3.DeclaringType.Scope == module2 && redirectedNamespaces.Contains(baseDeclaringType(method3.DeclaringType).Namespace))
			{
				if (method3.Name == ".cctor")
				{
					ConstructorInfo[] array = patchingAssembly.GetType(method3.DeclaringType.FullName)?.GetConstructors(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					if (array != null && array.Length == 1)
					{
						return module2.ImportReference(array[0]);
					}
				}
				else if (method3.Name == ".ctor")
				{
					ConstructorInfo constructorInfo = patchingAssembly.GetType(method3.DeclaringType.FullName)?.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(CompareMethods);
					if ((object)constructorInfo != null)
					{
						return module2.ImportReference(constructorInfo);
					}
				}
				else
				{
					MethodInfo methodInfo = patchingAssembly.GetType(method3.DeclaringType.FullName)?.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault(delegate(MethodInfo m)
					{
						if (m.Name != method3.Name)
						{
							return false;
						}
						return (method3.ReturnType.ContainsGenericParameter == m.ReturnType.IsGenericParameter || AreSame(method3.ReturnType, module2.ImportReference(m.ReturnType))) && CompareMethods(m);
					});
					if ((object)methodInfo != null)
					{
						MethodReference methodReference3 = module2.ImportReference(methodInfo);
						if (method3 is GenericInstanceMethod genericInstanceMethod)
						{
							GenericInstanceMethod genericInstanceMethod2 = new GenericInstanceMethod(methodReference3);
							for (int k = 0; k < genericInstanceMethod.GenericArguments.Count; k++)
							{
								genericInstanceMethod.GenericArguments[k] = VisitType(genericInstanceMethod.GenericArguments[k], method3.FullName);
								genericInstanceMethod2.GenericArguments.Add(genericInstanceMethod.GenericArguments[k]);
							}
							methodReference3 = genericInstanceMethod2;
						}
						return methodReference3;
					}
				}
			}
			return null;
			bool CompareMethods(MethodBase m)
			{
				ParameterInfo[] parameters = m.GetParameters();
				if (method3.IsGenericInstance != m.IsGenericMethodDefinition || parameters.Length != 0 != method3.HasParameters)
				{
					return false;
				}
				if (method3.HasParameters)
				{
					if (method3.Parameters.Count != parameters.Length)
					{
						return false;
					}
					for (int l = 0; l < method3.Parameters.Count; l++)
					{
						if (parameters[l].ParameterType.IsGenericParameter ? (!method3.Parameters[l].ParameterType.IsGenericParameter) : (!AreSame(method3.Parameters[l].ParameterType, module2.ImportReference(parameters[l].ParameterType))))
						{
							return false;
						}
					}
				}
				return true;
			}
		}
		void VisitField(FieldReference? field, string referencingEntityName)
		{
			if (field != null)
			{
				field.FieldType = VisitType(field.FieldType, referencingEntityName);
				if (!(field is FieldDefinition))
				{
					field.DeclaringType = VisitType(field.DeclaringType, referencingEntityName);
				}
			}
		}
		void VisitMethod(MethodReference? method, string referencingEntityName)
		{
			if (method != null)
			{
				if (method is GenericInstanceMethod genericInstance2)
				{
					DispatchGenericArguments(genericInstance2, referencingEntityName);
				}
				method.ReturnType = VisitType(method.ReturnType, referencingEntityName);
				foreach (ParameterDefinition parameter2 in method.Parameters)
				{
					parameter2.ParameterType = VisitType(parameter2.ParameterType, referencingEntityName);
				}
				if (!(method is MethodSpecification))
				{
					method.DeclaringType = VisitType(method.DeclaringType, referencingEntityName);
				}
			}
		}
		TypeReference? VisitType(TypeReference? type, string referencingEntityName)
		{
			if (type == null)
			{
				return type;
			}
			if (type.GetElementType().IsGenericParameter)
			{
				return type;
			}
			if (type is GenericInstanceType genericInstance3)
			{
				DispatchGenericArguments(genericInstance3, referencingEntityName);
			}
			return FixupType(type);
		}
	}

	public static bool PreventHarmonyInteropFixLoad(Assembly? __0)
	{
		return __0 == null;
	}

	public static void Patch(IEnumerable<string>? extraNamespaces = null)
	{
		Harmony harmony = new Harmony("org.bepinex.plugins.APIManager");
		harmony.Patch(AccessTools.DeclaredMethod(typeof(PluginInfo), "ToString"), new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Patcher), "GrabPluginInfo")));
		harmony.Patch(AccessTools.DeclaredMethod(typeof(Assembly), "LoadFile", new Type[1] { typeof(string) }), new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Patcher), "InterceptAssemblyLoadFile")));
		harmony.Patch(AccessTools.DeclaredMethod(typeof(Assembly), "LoadFile", new Type[1] { typeof(string) }), new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Patcher), "CheckAssemblyLoadFile")));
		new PatchClassProcessor(harmony, typeof(AssemblyLoadInterceptor), allowUnannotatedType: true).Patch();
		Type type = typeof(AssemblyPatcher).Assembly.GetType("BepInEx.Preloader.RuntimeFixes.HarmonyInteropFix");
		if ((object)type != null)
		{
			harmony.Patch(AccessTools.DeclaredMethod(type, "OnAssemblyLoad"), new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Patcher), "PreventHarmonyInteropFixLoad")));
		}
		IEnumerable<TypeInfo> source;
		try
		{
			source = patchingAssembly.DefinedTypes.ToList();
		}
		catch (ReflectionTypeLoadException ex)
		{
			source = from t in ex.Types
				where t != null
				select t.GetTypeInfo();
		}
		BaseUnityPlugin baseUnityPlugin = (BaseUnityPlugin)Chainloader.ManagerObject.GetComponent(source.First((TypeInfo t) => t.IsClass && typeof(BaseUnityPlugin).IsAssignableFrom(t)));
		redirectedNamespaces = new HashSet<string>(extraNamespaces ?? Array.Empty<string>()) { baseUnityPlugin.GetType().Namespace };
		modGUID = baseUnityPlugin.Info.Metadata.GUID;
	}
}

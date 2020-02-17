/*
 * Created by SharpDevelop.
 * User: Malte Schulze
 * Date: 24.06.2019
 * Time: 16:31
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Verse;

namespace RIMMSAssemble
{
	/// <summary>
	/// Description of RIMMSAssemble.
	/// </summary>
	public class RIMMSAssemble : Mod
	{
		public RIMMSAssemble(ModContentPack content) : base(content) {
			//Log.Message("starting rimmsassemble");
			
			//moving the new handler to the front since rimworlds handler is bugged and breaks the processing
			EventInfo ei = typeof(AppDomain).GetEvent("AssemblyResolve");
			FieldInfo fi = typeof(AppDomain).GetField("AssemblyResolve", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			ResolveEventHandler eh = fi.GetValue(AppDomain.CurrentDomain) as ResolveEventHandler;
			List<Delegate> lst = new List<Delegate>(eh.GetInvocationList());
			foreach( Delegate del in lst ) {
				ei.GetRemoveMethod().Invoke(AppDomain.CurrentDomain, new object[] { del });
			}
			
			ResolveEventHandler reh = (object sender, ResolveEventArgs args) => {
				if ( args.Name.IndexOf(" Version=") < 0 ) return null;
				
				//Log.Message("Custom assembly resolver: "+sender+" "+args+" "+args.Name);
				
            	try {
					AssemblyName an = new AssemblyName(args.Name);
					an.Version = null;
					return Assembly.Load(an); 
				} catch (Exception exception) {
					Log.Error(exception.Message);
				}
				return null;
        	};
			
			AppDomain.CurrentDomain.AssemblyResolve += reh;
			
			foreach ( Delegate del in lst ) {
				ei.GetAddMethod().Invoke(AppDomain.CurrentDomain, new object[] { del });
			}
			
			//At this point assemblies that do not have types whose definition is not dependant on other assemblies will work.
			//However if a class is defined with a type dependancy the assembly loading crashed during earlier AssemblyIsUsable.
			//Therefore those assemblies must be renamed and only loaded after the assembly resolver is in place.
			//This also means we must take over the mod loading loop to ensure mods are loaded in proper order since assemblies are no longer loaded in order.
			List<Type> allExistingModClasses = new List<Type>(typeof(Mod).InstantiableDescendantsAndSelf());
			Dictionary<Assembly,ModContentPack> loadedAssemblies = new Dictionary<Assembly, ModContentPack>();
			MethodInfo miAssemblyIsUsable = typeof(ModAssemblyHandler).GetMethod("AssemblyIsUsable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			foreach ( ModContentPack modContentPack in LoadedModManager.RunningMods ) {
				ModAssemblyHandler ass = modContentPack.assemblies;
				Dictionary<string,FileInfo> assFilesInAssemblyFolder = ModContentPack.GetAllFilesForMod(modContentPack,"Assemblies/",s=>s.ToLower() == ".ass");
				if ( assFilesInAssemblyFolder == null || assFilesInAssemblyFolder.Count == 0 ) {
					continue;
				}
				foreach ( FileInfo fileInfo in assFilesInAssemblyFolder.Values ) {
					Assembly assembly = null;
					try {
						byte[] rawAssembly = File.ReadAllBytes(fileInfo.FullName);
						string fileName = Path.Combine(fileInfo.DirectoryName, Path.GetFileNameWithoutExtension(fileInfo.FullName)) + ".pdb";
						FileInfo fileInfo2 = new FileInfo(fileName);
						if (fileInfo2.Exists) {
							byte[] rawSymbolStore = File.ReadAllBytes(fileInfo2.FullName);
							assembly = AppDomain.CurrentDomain.Load(rawAssembly, rawSymbolStore);
						} else {
							assembly = AppDomain.CurrentDomain.Load(rawAssembly);
						}
					} catch (Exception ex) {
						Log.Error("Exception loading " + fileInfo.Name + ": " + ex.ToString(), false);
						break;
					}
					Log.Message("testing assembly: "+assembly+" "+ass.loadedAssemblies.Contains(assembly));
					if ( assembly != null && !ass.loadedAssemblies.Contains(assembly) ) {
						if ( (bool)miAssemblyIsUsable.Invoke(ass, new Object[]{assembly}) ) {
							ass.loadedAssemblies.Add(assembly);
							loadedAssemblies.Add(assembly,modContentPack);
							//Log.Message("Loading ass assembly: "+assembly.FullName);
						} else {
							Log.Message("Unusable ass assemble: "+assembly.FullName);
						}
					}
				}
			}
			
			if ( loadedAssemblies.Count > 0 ) {
				//Reordering the mod classes and initializing them in order while filling runningModClasses will end the initial loop in createmodclasses() and load mods in the right order.
				Log.Message("Found ass assemblies. Creating new mod classes via custom loop.");
				List<Type> modTypesSorted = new List<Type>();
				List<ModContentPack> modsOrdered = LoadedModManager.RunningModsListForReading;
				Dictionary<Type, Mod> runningModClasses = (Dictionary<Type, Mod>)typeof(LoadedModManager).GetField("runningModClasses", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
					.GetValue(null);
				foreach ( Type type in typeof(Mod).InstantiableDescendantsAndSelf() ) {
					if ( !runningModClasses.ContainsKey(type) && !modTypesSorted.Contains(type) && type != typeof(RIMMSAssemble) ) {
						modTypesSorted.Add(type);
					}
				}
				modTypesSorted.SortBy<Type,int>(t=> modsOrdered.FirstIndexOf(m=>m.assemblies.loadedAssemblies.Contains(t.Assembly)));
				
				List<ModContentPack>.Enumerator enumerator = modsOrdered.GetEnumerator();
				foreach ( Type type in modTypesSorted ) {
					DeepProfiler.Start("Loading " + type + " mod class");
					try
					{
						while ( enumerator.MoveNext() && !enumerator.Current.assemblies.loadedAssemblies.Contains(type.Assembly) ) {}
						if ( enumerator.Current != null ) {
							runningModClasses[type] = (Mod)Activator.CreateInstance(type, new object[]{enumerator.Current});
						} else {
							Log.Error("Failed to match ModContentPack to assembly!");
						}
					}
					catch (Exception ex)
					{
						Log.Error(string.Concat(new object[]
						{
							"Error while instantiating a mod of type ",
							type,
							": ",
							ex
						}), false);
					}
					finally
					{
						DeepProfiler.End();
					}
				}
			}/* else {
				Log.Message("No new assemblies found, continuing with normal loading.");
			}*/
		}
	}
}

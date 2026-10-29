using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace UniverseLib;

public class AssemblyCacheQueue
{
    public static Action<Assembly> OnAssemblyDequeue;

    private List<Assembly> AssemblyQueue = new();
    
    private static string AssemblyHash(AssemblyName assemblyName) => assemblyName.Name;

    private Assembly GetLoaded(AssemblyName assemblyName) => AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(assembly => AssemblyHash(assemblyName) == AssemblyHash(assembly.GetName()));

    public void Enqueue(Assembly assembly)
    {
        AssemblyQueue.Add(assembly);
        Update();
    }

    public void Update()
    {
        var dependencies = new HashSet<string>();
        var outOrder = new List<string>();
        
        var toRemove = new List<Assembly>();
        foreach (var asm in AssemblyQueue)
        {
            if (dependencies.Contains(AssemblyHash(asm.GetName())) || TryRemove(asm, dependencies, outOrder))
                toRemove.Add(asm);
        }

        foreach (var hash in outOrder)
        {
            var asm = toRemove.FirstOrDefault(asm => AssemblyHash(asm.GetName()) == hash);
            if (asm != null)
            {
                toRemove.Remove(asm);
                AssemblyQueue.Remove(asm);
                ReflectionUtility.CacheTypes(asm);
                OnAssemblyDequeue?.Invoke(asm);
            }
        }
        
        foreach (var asm in toRemove)
        {
            AssemblyQueue.Remove(asm);
            ReflectionUtility.CacheTypes(asm);
            OnAssemblyDequeue?.Invoke(asm);
        }
    }

    private bool TryRemove(Assembly assembly, HashSet<string> deps, List<string> outOrder)
    {
        deps.Add(AssemblyHash(assembly.GetName()));
        foreach (var dependency in assembly.GetReferencedAssemblies())
        {
            if (deps.Contains(AssemblyHash(dependency)))
                continue;

            var asm = GetLoaded(dependency);
            if (asm == null)
                return false;

            if (!TryRemove(asm, deps, outOrder))
                return false;
        }
        
        outOrder.Add(AssemblyHash(assembly.GetName()));
        return true;
    }
}
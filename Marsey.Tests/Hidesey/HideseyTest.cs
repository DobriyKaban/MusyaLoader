using System;
using System.Reflection;
using HarmonyLib;
using Marsey.Game.Managers;
using Marsey.Stealthsey;
using NUnit.Framework; 
using System.Linq;
using System.Collections.Generic;

namespace Marsey.HideseyTests;

[TestFixture]
public class HideseyTest
{
    private string HarmonyID = "com.marsey.tests"; 
    private Harmony harm;
        
    [OneTimeSetUp]
    public void SetUp()
    {
        harm = new Harmony(HarmonyID);
        HarmonyManager.Init(harm);
        Hidesey.Initialize();
        Hidesey.HidePatch(typeof(Hidesey).Assembly);
    }

    [Test]
    public void Hidesey_HiddenAssemblies()
    {
        // Arrange
        Hidesey.Disperse();
        Assembly[] filteredAssemblies;

        using (Hidesey.ForceEngineView())
        {
            filteredAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        }

        List<string> HiddenAssemblies = new List<string> { "Harmony", "Marsey", "MonoMod", "Mono." };
        
        Assembly[] foundForbidden = filteredAssemblies.Where(assembly => 
            assembly != Assembly.GetExecutingAssembly() &&
            HiddenAssemblies.Any(forbidden => assembly.FullName != null && assembly.FullName.Contains(forbidden))
        ).ToArray();

        // Assert
        Assert.That(foundForbidden, Is.Empty, "Forbidden assemblies were found in the engine view domain.");
    }
    
    [Test]
    public void Hidesey_HiddenTypes()
    {
        List<Type> allTypes = new List<Type>();

        using (Hidesey.ForceEngineView())
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == Assembly.GetExecutingAssembly()) continue;

                try
                {
                    allTypes.AddRange(assembly.GetTypes());
                }
                catch (ReflectionTypeLoadException e)
                {
                    allTypes.AddRange(e.Types.Where(t => t != null)!);
                }
                catch
                {
                }
            }
        }
        
        List<string> HiddenTypeNamespaces = new List<string> { "Marsey", "Harmony" };
        
        Type[] filteredTypes = allTypes.Where(type => 
            HiddenTypeNamespaces.Any(forbidden => type.Namespace?.StartsWith(forbidden) ?? false)
        ).ToArray();
        
        // Assert
        Assert.That(filteredTypes, Is.Empty, "Forbidden types were found in the engine view domain.");
    }

    [Test]
    public void Hidesey_ManifestHidesCvars()
    {
        Hidesey.Apply(new HideManifest {
            Cvars = { "stealth_cvar" }
        });

        List<string> originalCvars = new List<string> { "normal_cvar", "stealth_cvar", "another_cvar" };
        IEnumerable<string> filteredCvars;

        // Act
        using (Hidesey.ForceEngineView())
        {
            filteredCvars = Hidesey.LyingCvars(originalCvars);
        }

        // Assert
        Assert.That(filteredCvars, Does.Contain("normal_cvar"));
        Assert.That(filteredCvars, Does.Not.Contain("stealth_cvar"), "Cvar wasn't hidden by the manifest.");
    }

    [Test]
    public void Hidesey_HiddenDefinedTypes()
    {
        List<TypeInfo> allDefinedTypes = new List<TypeInfo>();

        using (Hidesey.ForceEngineView())
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == Assembly.GetExecutingAssembly()) continue;

                try
                {
                    allDefinedTypes.AddRange(assembly.DefinedTypes);
                }
                catch (ReflectionTypeLoadException e)
                {
                    allDefinedTypes.AddRange(e.Types.Where(t => t != null).Select(t => t.GetTypeInfo()));
                }
                catch
                {
                }
            }
        }

        List<string> hiddenTypeNamespaces = new List<string> { "Marsey", "Harmony" };

        TypeInfo[] filteredTypes = allDefinedTypes.Where(type =>
            hiddenTypeNamespaces.Any(forbidden => type.Namespace?.StartsWith(forbidden) ?? false)
        ).ToArray();

        // Assert
        Assert.That(filteredTypes, Is.Empty, "Forbidden defined types were found in the engine view domain.");
    }

    [Test]
    public void Hidesey_HiddenExceptionStackTrace()
    {
        Exception ex;
        try
        {
            throw new InvalidOperationException("Test exception from Marsey test");
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        string? stackTrace;
        using (Hidesey.ForceEngineView())
        {
            stackTrace = ex.StackTrace;
        }

        // Assert
        if (!string.IsNullOrEmpty(stackTrace))
        {
            Assert.That(stackTrace, Does.Not.Contain("0Harmony"));
            Assert.That(stackTrace, Does.Not.Contain("HarmonyLib"));
            Assert.That(stackTrace, Does.Not.Contain("Marsey"));
        }
    }

    [Test]
    public void Hidesey_HiddenTypeAssembly()
    {
        Type hiddenType = typeof(Hidesey);
        Assembly? asm;

        using (Hidesey.ForceEngineView())
        {
            asm = hiddenType.Assembly;
        }

        // Assert: under engine view, Type.Assembly should be spoofed to a surrogate assembly, not Marsey
        Assert.That(asm, Is.Not.Null);
        Assert.That(asm.GetName().Name, Is.Not.EqualTo("Marsey"), "Type.Assembly was not spoofed under engine view.");
    }
}
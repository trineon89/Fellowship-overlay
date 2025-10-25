using System;
using System.Reflection;
using NetSparkleUpdater;
using NetSparkleUpdater.UI.WPF;

namespace Fellowship_overlay.Services
{
    public static class Updater
    {
        private const string AppCastUrl = "https://trineon89.github.io/Fellowship-overlay/appcast.xml";

        public static SparkleUpdater Create()
        {
            var sparkle = CreateUpdaterInstance();

            TryAssignUiFactory(sparkle);
            return sparkle;
        }

        private static SparkleUpdater CreateUpdaterInstance()
        {
            foreach (var ctor in typeof(SparkleUpdater).GetConstructors())
            {
                var args = BuildConstructorArguments(ctor);
                if (args is null)
                {
                    continue;
                }

                try
                {
                    if (ctor.Invoke(args) is SparkleUpdater sparkle)
                    {
                        return sparkle;
                    }
                }
                catch
                {
                    // Try next constructor
                }
            }

            throw new InvalidOperationException("Unable to construct NetSparkleUpdater.SparkleUpdater with the available constructors.");
        }

        private static object?[]? BuildConstructorArguments(ConstructorInfo ctor)
        {
            var parameters = ctor.GetParameters();
            if (parameters.Length == 0)
            {
                return null;
            }

            var args = new object?[parameters.Length];

            if (!TryPopulateFirstParameter(parameters[0], args))
            {
                return null;
            }

            for (var i = 1; i < parameters.Length; i++)
            {
                args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
            }

            return args;
        }

        private static bool TryPopulateFirstParameter(ParameterInfo parameter, object?[] args)
        {
            if (parameter.ParameterType == typeof(string))
            {
                args[0] = AppCastUrl;
                return true;
            }

            if (parameter.ParameterType.FullName == "NetSparkleUpdater.AppCastHandlers.AppCastSettings")
            {
                var settings = CreateAppCastSettings(parameter.ParameterType);
                if (settings is null)
                {
                    return false;
                }

                args[0] = settings;
                return true;
            }

            return false;
        }

        private static object? CreateAppCastSettings(Type appCastSettingsType)
        {
            try
            {
                foreach (var ctor in appCastSettingsType.GetConstructors())
                {
                    var parameters = ctor.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    {
                        return ctor.Invoke(new object?[] { AppCastUrl });
                    }

                    if (parameters.Length == 0)
                    {
                        var instance = ctor.Invoke(Array.Empty<object?>());
                        var urlProperty = appCastSettingsType.GetProperty("AppCastUrl", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (urlProperty?.CanWrite == true)
                        {
                            urlProperty.SetValue(instance, AppCastUrl);
                        }

                        return instance;
                    }
                }

                var parameterless = appCastSettingsType.GetConstructor(Type.EmptyTypes);
                if (parameterless != null)
                {
                    var instance = parameterless.Invoke(Array.Empty<object?>());
                    var urlProperty = appCastSettingsType.GetProperty("AppCastUrl", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (urlProperty?.CanWrite == true)
                    {
                        urlProperty.SetValue(instance, AppCastUrl);
                    }

                    return instance;
                }
            }
            catch
            {
                // Give up and let the caller try another constructor
            }

            return null;
        }

        private static void TryAssignUiFactory(SparkleUpdater sparkle)
        {
            try
            {
                var factory = new UIFactory();
                if (sparkle.UIFactory is null)
                {
                    sparkle.UIFactory = factory;
                }
            }
            catch
            {
                // leave the default factory in place if construction fails
            }
        }
    }
}

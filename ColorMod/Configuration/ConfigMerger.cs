using System.ComponentModel;
using System.Linq;
using System.Reflection;
using FFTColorMod.Configuration;

namespace FFTColorMod.Configuration
{
    public static class ConfigMerger
    {
        public static Config MergeConfigs(Config existingConfig, Config incomingConfig)
        {
            var mergedConfig = new Config();

            // Handle generic job color schemes using dictionary approach
            foreach (var jobKey in mergedConfig.GetAllJobKeys())
            {
                var incomingValue = incomingConfig.GetColorScheme(jobKey);
                var existingValue = existingConfig.GetColorScheme(jobKey);

                // If incoming value is different from default (original), it was explicitly changed
                if (incomingValue != ColorScheme.original)
                {
                    mergedConfig.SetColorScheme(jobKey, incomingValue);
                }
                else
                {
                    // Otherwise preserve the existing value
                    mergedConfig.SetColorScheme(jobKey, existingValue);
                }
            }

            // Handle story characters separately (they still have properties)
            // Agrias
            if (incomingConfig.Agrias != AgriasColorScheme.original)
            {
                mergedConfig.Agrias = incomingConfig.Agrias;
            }
            else
            {
                mergedConfig.Agrias = existingConfig.Agrias;
            }

            // Orlandeau
            if (incomingConfig.Orlandeau != OrlandeauColorScheme.original)
            {
                mergedConfig.Orlandeau = incomingConfig.Orlandeau;
            }
            else
            {
                mergedConfig.Orlandeau = existingConfig.Orlandeau;
            }

            // Cloud
            if (incomingConfig.Cloud != CloudColorScheme.original)
            {
                mergedConfig.Cloud = incomingConfig.Cloud;
            }
            else
            {
                mergedConfig.Cloud = existingConfig.Cloud;
            }

            // Mustadio
            if (incomingConfig.Mustadio != MustadioColorScheme.original)
            {
                mergedConfig.Mustadio = incomingConfig.Mustadio;
            }
            else
            {
                mergedConfig.Mustadio = existingConfig.Mustadio;
            }

            // Reis
            if (incomingConfig.Reis != ReisColorScheme.original)
            {
                mergedConfig.Reis = incomingConfig.Reis;
            }
            else
            {
                mergedConfig.Reis = existingConfig.Reis;
            }

            // Malak
            if (incomingConfig.Malak != MalakColorScheme.original)
            {
                mergedConfig.Malak = incomingConfig.Malak;
            }
            else
            {
                mergedConfig.Malak = existingConfig.Malak;
            }

            // Rafa
            if (incomingConfig.Rafa != RafaColorScheme.original)
            {
                mergedConfig.Rafa = incomingConfig.Rafa;
            }
            else
            {
                mergedConfig.Rafa = existingConfig.Rafa;
            }

            // Delita
            if (incomingConfig.Delita != DelitaColorScheme.original)
            {
                mergedConfig.Delita = incomingConfig.Delita;
            }
            else
            {
                mergedConfig.Delita = existingConfig.Delita;
            }

            // Alma
            if (incomingConfig.Alma != AlmaColorScheme.original)
            {
                mergedConfig.Alma = incomingConfig.Alma;
            }
            else
            {
                mergedConfig.Alma = existingConfig.Alma;
            }

            // Wiegraf
            if (incomingConfig.Wiegraf != WiegrafColorScheme.original)
            {
                mergedConfig.Wiegraf = incomingConfig.Wiegraf;
            }
            else
            {
                mergedConfig.Wiegraf = existingConfig.Wiegraf;
            }

            // Celia
            if (incomingConfig.Celia != CeliaColorScheme.original)
            {
                mergedConfig.Celia = incomingConfig.Celia;
            }
            else
            {
                mergedConfig.Celia = existingConfig.Celia;
            }

            // Lettie
            if (incomingConfig.Lettie != LettieColorScheme.original)
            {
                mergedConfig.Lettie = incomingConfig.Lettie;
            }
            else
            {
                mergedConfig.Lettie = existingConfig.Lettie;
            }

            return mergedConfig;
        }
    }
}
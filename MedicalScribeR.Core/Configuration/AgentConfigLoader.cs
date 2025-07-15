using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MedicalScribeR.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MedicalScribeR.Core.Configuration
{
    public class AgentConfigLoader
    {
        public Dictionary<string, AgentConfiguration> LoadConfig(string basePath)
        {
            var jsonConfigPath = Path.Combine(basePath, "agents.json");
            var yamlConfigPath = Path.Combine(basePath, "agents.yaml");

            if (File.Exists(jsonConfigPath))
            {
                var json = File.ReadAllText(jsonConfigPath);
                return JsonSerializer.Deserialize<Dictionary<string, AgentConfiguration>>(json);
            }
            else if (File.Exists(yamlConfigPath))
            {
                var yaml = File.ReadAllText(yamlConfigPath);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                return deserializer.Deserialize<Dictionary<string, AgentConfiguration>>(yaml);
            }
            else
            {
                throw new FileNotFoundException("Could not find agents.json or agents.yaml in the configuration directory.");
            }
        }
    }
}
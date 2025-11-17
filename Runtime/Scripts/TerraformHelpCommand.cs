// Copyright 2025 Spellbound Studio Inc.

using System.Text;
using Spellbound.Core.Console;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Help command that lists all terraform-related utility commands.
    /// </summary>
    [ConsoleCommandClass("terraform", "tf")]
    public class TerraformHelpCommand : ICommand {
        public string Name => "terraform";
        public string Description => "List all terraform commands";
        public string Usage => "terraform";

        public CommandResult Execute(string[] args) {
            // Get all commands from SbTerrain class
            var commands = 
                    PresetCommandRegistry.GetUtilityCommandsByClass(typeof(SbTerrain));

            var sb = new StringBuilder();
            
            sb.AppendLine("=== Terraform Commands ===");
            sb.AppendLine();

            foreach (var (commandName, description) in commands)
                sb.AppendLine($"{commandName,-25} {description}");

            return CommandResult.Ok(sb.ToString());
        }
    }
}
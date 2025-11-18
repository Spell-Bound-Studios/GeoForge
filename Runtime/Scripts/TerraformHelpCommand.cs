// Copyright 2025 Spellbound Studio Inc.

using System.Text;
using Spellbound.Core.Console;

namespace Spellbound.MarchingCubes {
    /// <summary>
    /// Help command that lists all terraform-related utility commands.
    /// My intention was to register all commands that are registered under the SbTerrain class... I chose to implement
    /// it into its own class (TerraformHelpCommand) just in case SbTerrain changes later, or I want to change the way
    /// the API is accessed at a later date.
    /// </summary>
    [ConsoleCommandClass("terraform", "tf")]
    public class TerraformHelpCommand : ICommand {
        public string Name => "terraform";
        public string Description => "List all terraform commands";
        public string Usage => "terraform";

        /// <summary>
        /// This is implemented by the ICommand interface and allows us to implement our own logic for this class.
        /// </summary>
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
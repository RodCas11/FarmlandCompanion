using System;
using RagnarokCompanion.Services;
namespace RagnarokCompanion.ViewModels;
public sealed partial class MainViewModel
{
    public UpdateViewModel Updates {get;}=new();
    public string[] ThemeChoices=>AppearanceService.Choices;
    public string SelectedTheme { get=>Settings.Theme; set { Settings.Theme=value;Save();AppearanceService.Apply(value,DateTimeOffset.UtcNow);Changed();Changed(nameof(SeasonDecoration)); } }
    public string SeasonDecoration=>AppearanceService.IsHalloween(Settings.Theme,DateTimeOffset.UtcNow)?"🎃  🦇  🕸":"";
}
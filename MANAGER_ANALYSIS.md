# Manager Class Analysis for Consolidation

## Current Manager Classes (7 identified)

### 1. **ConfigurationManager**
- **Location**: `ColorMod\Configuration\ConfigurationManager.cs`
- **Responsibilities**:
  - Load/Save Config.json
  - Cache configuration
  - Reset to defaults
  - Get available color schemes
- **Dependencies**: JobClassDefinitionService

### 2. **ThemeManager**
- **Location**: `ColorMod\Services\ThemeManager.cs`
- **Responsibilities**:
  - Apply initial themes
  - Cycle themes (Orlandeau, Agrias, Cloud, etc.)
  - Manage StoryCharacterThemeManager
- **Dependencies**: StoryCharacterThemeManager

### 3. **StoryCharacterThemeManager**
- **Location**: `ColorMod\Utilities\StoryCharacterThemeManager.cs`
- **Responsibilities**:
  - Track current themes for story characters
  - Cycle through available themes
  - Map character names to themes
- **Dependencies**: None (uses file system directly)

### 4. **ConfigBasedSpriteManager**
- **Location**: `ColorMod\Utilities\ConfigBasedSpriteManager.cs`
- **Responsibilities**:
  - Apply configuration to sprites
  - Copy sprite files based on themes
  - Handle generic job themes
  - Handle story character themes
- **Dependencies**: ConfigurationManager, CharacterDefinitionService

### 5. **SpriteFileManager**
- **Location**: `ColorMod\Utilities\SpriteFileManager.cs`
- **Responsibilities**:
  - Copy sprite files
  - Clear sprite directories
  - Apply specific color schemes
- **Dependencies**: File system

### 6. **PreviewImageManager**
- **Location**: `ColorMod\Configuration\UI\PreviewImageManager.cs`
- **Responsibilities**:
  - Load preview images for UI
  - Cache preview images
  - Get preview for theme/character combinations
- **Dependencies**: File system

### 7. **DynamicSpriteLoader** (not found in grep, but referenced in Mod.cs)
- **Location**: `ColorMod\Utilities\DynamicSpriteLoader.cs`
- **Responsibilities**: Runtime sprite loading

## Proposed Consolidation into 3 Core Services

### 1. **IConfigurationService**
Combines: ConfigurationManager + configuration aspects of other managers

**Responsibilities**:
- Load/Save configuration
- Cache management
- Default values
- Configuration validation
- Settings persistence

**Methods**:
```csharp
interface IConfigurationService
{
    Config LoadConfig();
    void SaveConfig(Config config);
    Config GetDefaultConfig();
    void ResetToDefaults();
    bool ValidateConfig(Config config);
}
```

### 2. **IThemeService**
Combines: ThemeManager + StoryCharacterThemeManager + theme logic from ConfigBasedSpriteManager

**Responsibilities**:
- Theme selection and cycling
- Theme application
- Theme discovery
- Character-to-theme mapping
- Available themes listing

**Methods**:
```csharp
interface IThemeService
{
    void ApplyTheme(string characterName, string themeName);
    string CycleTheme(string characterName);
    IEnumerable<string> GetAvailableThemes(string characterName);
    string GetCurrentTheme(string characterName);
    void ApplyConfigurationThemes(Config config);
}
```

### 3. **ISpriteService**
Combines: SpriteFileManager + ConfigBasedSpriteManager + DynamicSpriteLoader + sprite aspects of PreviewImageManager

**Responsibilities**:
- Sprite file operations
- Dynamic sprite loading
- Preview image management
- Sprite caching
- File copying and management

**Methods**:
```csharp
interface ISpriteService
{
    void CopySprites(string theme, string character);
    void ClearSprites();
    Image GetPreviewImage(string character, string theme);
    void ApplySpriteConfiguration(Config config);
    void LoadDynamicSprites(string path);
}
```

## Benefits of Consolidation

1. **Reduced Complexity**: 7 managers → 3 services
2. **Clear Boundaries**: Each service has distinct responsibilities
3. **Better Testing**: Interfaces make mocking easier
4. **Reduced Dependencies**: Fewer circular references
5. **Single Responsibility**: Each service focuses on one domain

## Migration Strategy

1. **Phase 1**: Create interfaces (TDD approach)
2. **Phase 2**: Write tests for new services
3. **Phase 3**: Implement services using existing manager code
4. **Phase 4**: Update consumers to use new services
5. **Phase 5**: Remove old manager classes

## Dependency Graph

### Current (Complex):
```
ConfigurationManager → JobClassDefinitionService
ThemeManager → StoryCharacterThemeManager → File System
ConfigBasedSpriteManager → ConfigurationManager, CharacterDefinitionService
SpriteFileManager → File System
PreviewImageManager → File System
```

### Proposed (Simplified):
```
IConfigurationService → IPathResolver
IThemeService → IConfigurationService, CharacterDefinitionService
ISpriteService → IPathResolver, IThemeService
```

This consolidation will significantly improve code maintainability and testability.
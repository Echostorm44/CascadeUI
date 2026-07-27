package com.cascadeui.rider

/**
 * New Solution wizard step for creating Cascade UI projects.
 * Presents theme, template, and configuration options.
 */
class NewSolutionWizard {

    data class WizardConfig(
        val theme: String = "AppleTheme",
        val themeMode: String = "Light",
        val template: String = "cascade-app-blank",
        val enableLocalization: Boolean = false,
        val enableAi: Boolean = false
    )

    var config: WizardConfig = WizardConfig()
        private set

    fun configure(newConfig: WizardConfig) {
        config = newConfig
    }

    /**
     * Generates the dotnet new command for project creation.
     */
    fun generateCommand(projectName: String, outputDir: String): String {
        val args = buildList {
            add("dotnet")
            add("new")
            add(config.template)
            add("-n")
            add(quoteArg(projectName))
            add("-o")
            add(quoteArg(outputDir))
            if (config.enableLocalization) add("--localization")
            if (config.enableAi) add("--ai")
        }
        return args.joinToString(" ")
    }

    private fun quoteArg(value: String): String {
        return if (value.contains(' ') || value.contains('"')) {
            "\"${value.replace("\"", "\\\"")}\""
        } else {
            value
        }
    }

    companion object {
        val AVAILABLE_TEMPLATES = listOf(
            TemplateInfo("cascade-app-blank", "Cascade UI App (Blank)", "An empty Cascade UI application"),
            TemplateInfo("cascade-app-nav", "Cascade UI App (Navigation)", "Application with sidebar navigation"),
            TemplateInfo("cascade-lib", "Cascade UI Library", "Reusable component library"),
            TemplateInfo("cascade-controls", "Cascade UI Control Library", "Custom controls with theme support")
        )

        val AVAILABLE_THEMES = listOf("AppleTheme", "FluentTheme", "Material3Theme")
        val AVAILABLE_MODES = listOf("Light", "Dark", "Auto")
    }
}

data class TemplateInfo(
    val id: String,
    val name: String,
    val description: String
)

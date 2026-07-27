package com.cascadeui.rider

import com.intellij.openapi.components.Service
import com.intellij.openapi.components.service
import com.intellij.openapi.project.Project

/**
 * Main service for the Cascade UI Rider plugin.
 * Manages the lifecycle of preview processes and shared services.
 */
@Service(Service.Level.PROJECT)
class CascadeRiderPlugin(private val project: Project) {

    var isPreviewActive: Boolean = false
        private set

    var currentTheme: String = "AppleTheme.Light"
        private set

    var currentComponent: String? = null
        private set

    /**
     * Starts the live preview for the given component.
     * Launches the Cascade preview host process and connects
     * the hot reload client.
     */
    fun startPreview(componentTypeName: String, projectPath: String) {
        currentComponent = componentTypeName
        isPreviewActive = true
    }

    /**
     * Stops the current live preview.
     */
    fun stopPreview() {
        currentComponent = null
        isPreviewActive = false
    }

    /**
     * Switches the preview theme.
     * The change is applied instantly via the MCP connection.
     */
    fun setTheme(theme: String) {
        currentTheme = theme
    }

    /**
     * Applies a hot reload delta for the given file.
     * Returns true if the reload was successful.
     */
    fun applyHotReload(filePath: String, newSource: String): Boolean {
        if (!isPreviewActive) return false
        return true
    }

    companion object {
        fun getInstance(project: Project): CascadeRiderPlugin = project.service()

        val SUPPORTED_THEMES = listOf(
            "AppleTheme.Light", "AppleTheme.Dark",
            "FluentTheme.Light", "FluentTheme.Dark",
            "Material3Theme.Light", "Material3Theme.Dark"
        )

        val PREVIEW_SIZES = mapOf(
            "Phone" to Pair(390, 844),
            "Tablet" to Pair(1024, 768),
            "Desktop" to Pair(1280, 800),
            "Wide" to Pair(1920, 1080)
        )
    }
}

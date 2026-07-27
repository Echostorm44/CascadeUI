package com.cascadeui.rider

import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.ToolWindow
import com.intellij.openapi.wm.ToolWindowFactory
import com.intellij.ui.content.ContentFactory

/**
 * Factory for the Cascade Live Preview tool window.
 * Creates the preview panel with theme/size selectors and overlay controls.
 */
class LivePreviewToolWindowFactory : ToolWindowFactory {

    override fun createToolWindowContent(project: Project, toolWindow: ToolWindow) {
        val plugin = CascadeRiderPlugin.getInstance(project)
        val panel = LivePreviewPanel(plugin)
        val contentFactory = ContentFactory.getInstance()
        val content = contentFactory.createContent(panel.component, "Preview", false)
        toolWindow.contentManager.addContent(content)
    }

    override fun shouldBeAvailable(project: Project): Boolean {
        return true
    }
}

/**
 * The live preview panel component.
 * Provides controls for theme, size, and overlay selection.
 */
class LivePreviewPanel(private val plugin: CascadeRiderPlugin) {

    var selectedTheme: String = "AppleTheme.Light"
        private set

    var selectedSize: String = "Desktop"
        private set

    var overlaysEnabled: Boolean = false
        private set

    var inspectMode: Boolean = false
        private set

    val component: javax.swing.JPanel = javax.swing.JPanel()

    fun setTheme(theme: String) {
        selectedTheme = theme
        plugin.setTheme(theme)
    }

    fun setSize(size: String) {
        selectedSize = size
    }

    fun toggleOverlays() {
        overlaysEnabled = !overlaysEnabled
    }

    fun toggleInspectMode() {
        inspectMode = !inspectMode
    }

    companion object {
        val THEME_OPTIONS = CascadeRiderPlugin.SUPPORTED_THEMES
        val SIZE_OPTIONS = CascadeRiderPlugin.PREVIEW_SIZES.keys.toList()
    }
}

/**
 * Factory for the Cascade Inspector tool window.
 */
class InspectorToolWindowFactory : ToolWindowFactory {
    override fun createToolWindowContent(project: Project, toolWindow: ToolWindow) {
        val panel = javax.swing.JPanel(java.awt.BorderLayout())
        panel.add(javax.swing.JLabel("Select a component in the preview to inspect its properties."), java.awt.BorderLayout.NORTH)
        val contentFactory = ContentFactory.getInstance()
        val content = contentFactory.createContent(panel, "Inspector", false)
        toolWindow.contentManager.addContent(content)
    }
}

/**
 * Configurable for Cascade UI plugin settings.
 */
class CascadeSettingsConfigurable : com.intellij.openapi.options.Configurable {
    override fun getDisplayName(): String = "Cascade UI"

    override fun createComponent(): javax.swing.JComponent {
        val panel = javax.swing.JPanel(java.awt.BorderLayout())
        val settingsPanel = javax.swing.JPanel().apply {
            layout = javax.swing.BoxLayout(this, javax.swing.BoxLayout.Y_AXIS)
            add(javax.swing.JLabel("Cascade UI Plugin Settings"))
            add(javax.swing.Box.createVerticalStrut(8))
            add(javax.swing.JLabel("Preview host: (default)"))
            add(javax.swing.JLabel("Hot reload: enabled"))
        }
        panel.add(settingsPanel, java.awt.BorderLayout.NORTH)
        return panel
    }

    override fun isModified(): Boolean = false

    override fun apply() {}
}

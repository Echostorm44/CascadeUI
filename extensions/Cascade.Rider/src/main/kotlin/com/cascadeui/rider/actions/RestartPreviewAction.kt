package com.cascadeui.rider.actions

import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.cascadeui.rider.CascadeRiderPlugin

class RestartPreviewAction : AnAction() {
    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val plugin = CascadeRiderPlugin.getInstance(project)
        val component = plugin.currentComponent ?: return
        val projectPath = project.basePath ?: return
        plugin.stopPreview()
        plugin.startPreview(component, projectPath)
    }

    override fun update(e: AnActionEvent) {
        val project = e.project ?: return
        val plugin = CascadeRiderPlugin.getInstance(project)
        e.presentation.isEnabled = plugin.isPreviewActive
    }
}

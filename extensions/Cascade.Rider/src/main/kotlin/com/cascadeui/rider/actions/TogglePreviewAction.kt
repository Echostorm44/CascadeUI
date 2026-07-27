package com.cascadeui.rider.actions

import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.cascadeui.rider.CascadeRiderPlugin

class TogglePreviewAction : AnAction() {
    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val plugin = CascadeRiderPlugin.getInstance(project)
        if (plugin.isPreviewActive) {
            plugin.stopPreview()
        } else {
            plugin.startPreview("", project.basePath ?: "")
        }
    }

    override fun update(e: AnActionEvent) {
        e.presentation.isEnabled = e.project != null
    }
}

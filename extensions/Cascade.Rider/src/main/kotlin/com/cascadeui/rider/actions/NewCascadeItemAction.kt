package com.cascadeui.rider.actions

import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.ui.Messages

class NewCascadeItemAction : AnAction() {
    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val templateName = e.presentation.text ?: "Cascade Item"
        val name = Messages.showInputDialog(
            project,
            "Enter name for the new $templateName:",
            "New $templateName",
            null
        ) ?: return

        // The actual file generation is handled by dotnet new item templates
        // invoked via the Cascade.IDE.Shared backend
    }

    override fun update(e: AnActionEvent) {
        e.presentation.isEnabled = e.project != null
    }
}

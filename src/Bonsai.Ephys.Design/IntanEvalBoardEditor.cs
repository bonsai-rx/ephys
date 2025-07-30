using System;
using Bonsai.Design;
using System.ComponentModel;
using System.Windows.Forms;

namespace Bonsai.Ephys.Design
{
    /// <summary>
    /// Provides a user interface for testing the impedance of individual channels in
    /// a RHA2000-EVAL board.
    /// </summary>
    public class IntanEvalBoardEditor : WorkflowComponentEditor
    {
        IntanEvalBoardEditorForm editorForm;

        /// <inheritdoc/>
        public override bool EditComponent(ITypeDescriptorContext context, object component, IServiceProvider provider, IWin32Window owner)
        {
            if (provider != null)
            {
                var editorService = (IWorkflowEditorState)provider.GetService(typeof(IWorkflowEditorState));
                if (editorService != null && !editorService.WorkflowRunning)
                {
                    if (editorForm == null)
                    {
                        editorForm = new IntanEvalBoardEditorForm();
                        editorForm.Source = (IntanEvalBoard)component;
                        EventHandler workflowStartedHandler = (sender, e) => editorForm.Close();
                        editorService.WorkflowStarted += workflowStartedHandler;
                        editorForm.FormClosed += (sender, e) =>
                        {
                            editorService.WorkflowStarted -= workflowStartedHandler;
                            editorForm = null;
                        };

                        editorForm.Show(owner);
                    }

                    editorForm.Activate();
                    return true;
                }
            }

            return false;
        }
    }
}

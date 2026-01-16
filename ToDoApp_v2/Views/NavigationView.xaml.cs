using System;
using SlimeTodo.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SlimeTodo.Views;

public partial class NavigationView : UserControl
{
    private System.Windows.Point _projectDragStartPoint;

    public NavigationView()
    {
        InitializeComponent();
    }

    private void NavButton_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent("TaskItem"))
        {
            e.Effects = System.Windows.DragDropEffects.Move;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    // Add Project TextBox handlers
    private void AddProjectTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            if (e.Key == Key.Enter)
            {
                vm.AddProjectCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                vm.CancelAddProjectCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void AddProjectTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                textBox.Focus();
                Keyboard.Focus(textBox);
            });
        }
    }

    // Edit Project TextBox handlers
    private void EditProjectTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is ProjectViewModel projectVm)
        {
            if (e.Key == Key.Enter)
            {
                projectVm.SaveEditCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                projectVm.CancelEditCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void EditProjectTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is ProjectViewModel projectVm)
        {
            if (projectVm.IsEditing)
            {
                projectVm.SaveEditCommand.Execute(null);
            }
        }
    }

    // Project Drag and Drop handlers
    private void ProjectButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _projectDragStartPoint = e.GetPosition(null);
    }

    private void ProjectButton_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var currentPosition = e.GetPosition(null);
        var diff = _projectDragStartPoint - currentPosition;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (sender is System.Windows.Controls.Button button && button.DataContext is ProjectViewModel projectVm)
        {
            // 편집 중이면 드래그 안함
            if (projectVm.IsEditing) return;

            var data = new System.Windows.DataObject("ProjectItem", projectVm);
            DragDrop.DoDragDrop(button, data, System.Windows.DragDropEffects.Move);
        }
    }

    private void ProjectButton_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent("TaskItem"))
        {
            e.Effects = System.Windows.DragDropEffects.Move;
        }
        else if (e.Data.GetDataPresent("ProjectItem"))
        {
            e.Effects = System.Windows.DragDropEffects.Move;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void ProjectButton_Drop(object sender, System.Windows.DragEventArgs e)
    {
        // Task 드롭 처리
        if (e.Data.GetDataPresent("TaskItem"))
        {
            if (sender is System.Windows.Controls.Button button && button.DataContext is ProjectViewModel projectVm)
            {
                var taskVm = e.Data.GetData("TaskItem") as TaskItemViewModel;
                if (taskVm != null && DataContext is MainViewModel mainVm)
                {
                    mainVm.MoveTaskToProject(taskVm, projectVm.Id);
                }
            }
            e.Handled = true;
            return;
        }

        // Project 드롭 처리 (순서 변경)
        if (e.Data.GetDataPresent("ProjectItem"))
        {
            if (sender is System.Windows.Controls.Button button && button.DataContext is ProjectViewModel targetProjectVm)
            {
                var draggedProjectVm = e.Data.GetData("ProjectItem") as ProjectViewModel;
                if (draggedProjectVm != null && draggedProjectVm.Id != targetProjectVm.Id && DataContext is MainViewModel mainVm)
                {
                    mainVm.ReorderProject(draggedProjectVm.Id, targetProjectVm.Id);
                }
            }
            e.Handled = true;
        }
    }

    // Navigation button drop handlers (for moving task out of project)
    private void NavButton_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TaskItem")) return;

        var taskVm = e.Data.GetData("TaskItem") as TaskItemViewModel;
        if (taskVm != null && DataContext is MainViewModel mainVm)
        {
            // Move task out of project (to inbox/regular views)
            mainVm.MoveTaskFromProject(taskVm);
        }
        e.Handled = true;
    }

    // HashTag color dot click - open context menu
    private void HashTagColorDot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }
}

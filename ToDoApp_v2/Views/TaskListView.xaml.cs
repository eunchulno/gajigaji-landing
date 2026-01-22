using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlimeTodo.ViewModels;

namespace SlimeTodo.Views;

public partial class TaskListView : UserControl
{
    private System.Windows.Point _dragStartPoint;

    public TaskListView()
    {
        InitializeComponent();
    }

    private void TaskList_KeyDown(object sender, KeyEventArgs e)
    {
        // TextBox에 포커스가 있으면 단축키 무시
        if (Keyboard.FocusedElement is TextBox)
            return;

        if (TaskList.SelectedItem is TaskItemViewModel selectedTask &&
            (selectedTask.IsEditing || selectedTask.IsAddingSubTask))
            return;

        // J/K 네비게이션
        if (e.Key == Key.J)
        {
            if (TaskList.SelectedIndex < TaskList.Items.Count - 1)
                TaskList.SelectedIndex++;
            e.Handled = true;
            return;
        }
        if (e.Key == Key.K)
        {
            if (TaskList.SelectedIndex > 0)
                TaskList.SelectedIndex--;
            e.Handled = true;
            return;
        }

        if (TaskList.SelectedItem is not TaskItemViewModel task) return;

        switch (e.Key)
        {
            case Key.Space:
                task.ToggleCompleteCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.S:
                task.ToggleImportantCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Delete:
                task.DeleteCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.E:
            case Key.F2:
                task.StartEditCommand.Execute(null);
                e.Handled = true;
                FocusEditTextBox();
                break;
        }
    }

    private void Title_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is TaskItemViewModel task)
        {
            if (e.ClickCount == 2)
            {
                // 더블클릭: 이름 수정
                task.StartEditCommand.Execute(null);
                e.Handled = true;
                FocusEditTextBox();
            }
            // 싱글클릭은 PreviewMouseLeftButtonDown에서 통합 처리 (메모 패널 + 서브태스크 확장)
        }
    }

    private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is TaskItemViewModel task)
        {
            if (e.Key == Key.Enter)
            {
                task.SaveEditCommand.Execute(null);
                e.Handled = true;
                TaskList.Focus();
            }
            else if (e.Key == Key.Escape)
            {
                task.CancelEditCommand.Execute(null);
                e.Handled = true;
                TaskList.Focus();
            }
        }
    }

    private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is TaskItemViewModel task && task.IsEditing)
        {
            task.SaveEditCommand.Execute(null);
        }
    }

    private void SubTaskTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is TaskItemViewModel task)
        {
            if (e.Key == Key.Enter)
            {
                task.AddSubTaskCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                task.CancelAddSubTaskCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void SubTaskInputTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.Focus();
        }
    }

    private void SubTaskTitle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement element && element.DataContext is SubTaskViewModel subTask)
        {
            subTask.StartEditCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void SubTaskEditTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is SubTaskViewModel subTask)
        {
            if (e.Key == Key.Enter)
            {
                subTask.SaveEditCommand.Execute(null);
                e.Handled = true;
                TaskList.Focus();
            }
            else if (e.Key == Key.Escape)
            {
                subTask.CancelEditCommand.Execute(null);
                e.Handled = true;
                TaskList.Focus();
            }
        }
    }

    private void SubTaskEditTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is SubTaskViewModel subTask && subTask.IsEditing)
        {
            subTask.SaveEditCommand.Execute(null);
        }
    }

    private void FocusEditTextBox()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (TaskList.SelectedItem != null)
            {
                var container = TaskList.ItemContainerGenerator.ContainerFromItem(TaskList.SelectedItem) as ListBoxItem;
                var textBox = FindVisualChild<TextBox>(container);
                if (textBox != null)
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }
            }
        }), System.Windows.Threading.DispatcherPriority.Render);
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;

            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }
        return null;
    }

    public void FocusList()
    {
        TaskList.Focus();
        if (TaskList.Items.Count > 0 && TaskList.SelectedItem == null)
        {
            TaskList.SelectedIndex = 0;
        }
    }

    private void HashTagAddButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.DataContext is TaskItemViewModel taskVm)
        {
            var contextMenu = button.ContextMenu;
            if (contextMenu != null)
            {
                contextMenu.ItemsSource = taskVm.AvailableHashTags;
                contextMenu.PlacementTarget = button;
                contextMenu.IsOpen = true;
            }
            e.Handled = true;
        }
    }

    #region Drag and Drop

    private void TaskList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);

        // Skip toggle if clicking on interactive elements (buttons, checkboxes, etc.)
        var originalSource = e.OriginalSource as DependencyObject;
        if (IsInteractiveElement(originalSource))
            return;

        // Handle task click - toggle note panel and subtasks
        var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (listBoxItem != null)
        {
            var clickedTask = listBoxItem.DataContext as TaskItemViewModel;
            if (clickedTask != null && DataContext is MainViewModel vm)
            {
                // Call ToggleNotePanel for both new and same task clicks
                vm.ToggleNotePanel(clickedTask);
            }
        }
    }

    private static bool IsInteractiveElement(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is System.Windows.Controls.Button ||
                element is System.Windows.Controls.CheckBox ||
                element is System.Windows.Controls.Primitives.ToggleButton ||
                element is System.Windows.Controls.ComboBox ||
                element is System.Windows.Controls.Calendar ||
                element is System.Windows.Controls.Primitives.CalendarItem ||
                element is System.Windows.Controls.Primitives.CalendarButton ||
                element is System.Windows.Controls.Primitives.CalendarDayButton ||
                element is System.Windows.Controls.DatePicker)
            {
                return true;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private void TaskList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var currentPosition = e.GetPosition(null);
        var diff = _dragStartPoint - currentPosition;

        // 최소 드래그 거리 체크
        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        // 편집 중이면 드래그 안함
        if (TaskList.SelectedItem is TaskItemViewModel task && task.IsEditing)
            return;

        // 드래그 시작
        var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (listBoxItem == null) return;

        var draggedItem = listBoxItem.DataContext as TaskItemViewModel;
        if (draggedItem == null) return;

        var data = new System.Windows.DataObject("TaskItem", draggedItem);
        DragDrop.DoDragDrop(listBoxItem, data, System.Windows.DragDropEffects.Move);
    }

    private void TaskList_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TaskItem"))
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;
    }

    private void TaskList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TaskItem")) return;

        var droppedData = e.Data.GetData("TaskItem") as TaskItemViewModel;
        if (droppedData == null) return;

        // 드롭 위치의 아이템 찾기
        var targetElement = e.OriginalSource as DependencyObject;
        var targetItem = FindAncestor<ListBoxItem>(targetElement);

        int newIndex;
        if (targetItem != null)
        {
            var targetData = targetItem.DataContext as TaskItemViewModel;
            newIndex = TaskList.Items.IndexOf(targetData);
        }
        else
        {
            // 빈 공간에 드롭하면 맨 끝으로
            newIndex = TaskList.Items.Count - 1;
        }

        // MainViewModel을 통해 재정렬
        if (DataContext is MainViewModel vm)
        {
            vm.ReorderTask(droppedData, newIndex);
        }

        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T result)
                return result;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    #endregion

    private void RecurrenceButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.Calendar calendar &&
            calendar.DataContext is TaskItemViewModel task &&
            e.AddedItems.Count > 0)
        {
            // 날짜가 선택되면 편집 모드 종료
            task.FinishEditDueDate();
        }
    }
}

using SlimeTodo.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SlimeTodo.Views;

public partial class WeekView : UserControl
{
    private System.Windows.Point _dragStartPoint;
    private bool _isDragging;
    private DateTime _lastClickTime = DateTime.MinValue;
    private object? _lastClickedItem;

    public WeekView()
    {
        InitializeComponent();
    }

    // Drag from Day Column tasks
    private void TaskItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private void TaskItem_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _isDragging = false;
            return;
        }

        var currentPos = e.GetPosition(null);
        var diff = _dragStartPoint - currentPos;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            if (!_isDragging && sender is Border border && border.Tag is TaskItemViewModel task)
            {
                _isDragging = true;

                var data = new System.Windows.DataObject();
                data.SetData("WeekTask", task);
                System.Windows.DragDrop.DoDragDrop(border, data, System.Windows.DragDropEffects.Move);

                _isDragging = false;
            }
        }
    }

    // Drag from Unscheduled tasks
    private void UnscheduledTask_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }

    private void UnscheduledTask_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _isDragging = false;
            return;
        }

        var currentPos = e.GetPosition(null);
        var diff = _dragStartPoint - currentPos;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            if (!_isDragging && sender is Border border && border.Tag is TaskItemViewModel task)
            {
                _isDragging = true;

                var data = new System.Windows.DataObject();
                data.SetData("UnscheduledTask", task);
                System.Windows.DragDrop.DoDragDrop(border, data, System.Windows.DragDropEffects.Move);

                _isDragging = false;
            }
        }
    }

    // Drop on Day Column
    private void DayColumn_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is Border border && border.DataContext is DayColumnViewModel dayVm)
        {
            // Reset visual state
            ResetBorderVisualState(border, dayVm);

            if (DataContext is WeekViewModel weekVm)
            {
                TaskItemViewModel? taskVm = null;

                if (e.Data.GetDataPresent("UnscheduledTask"))
                {
                    taskVm = e.Data.GetData("UnscheduledTask") as TaskItemViewModel;
                }
                else if (e.Data.GetDataPresent("WeekTask"))
                {
                    taskVm = e.Data.GetData("WeekTask") as TaskItemViewModel;
                }

                if (taskVm != null)
                {
                    weekVm.AssignTaskToDate(taskVm, dayVm.Date);
                }
            }
        }

        e.Handled = true;
    }

    private void DayColumn_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent("UnscheduledTask") || e.Data.GetDataPresent("WeekTask"))
        {
            e.Effects = System.Windows.DragDropEffects.Move;

            // Visual feedback
            if (sender is Border border)
            {
                border.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
                border.BorderThickness = new Thickness(2);
            }
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void DayColumn_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is Border border && border.DataContext is DayColumnViewModel dayVm)
        {
            ResetBorderVisualState(border, dayVm);
        }
    }

    private void ResetBorderVisualState(Border border, DayColumnViewModel dayVm)
    {
        if (dayVm.IsToday)
        {
            border.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
            border.BorderThickness = new Thickness(2);
        }
        else
        {
            border.BorderBrush = null;
            border.BorderThickness = new Thickness(0);
        }
    }

    // Add Task keyboard handler
    private void AddTaskTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is DayColumnViewModel dayVm)
        {
            if (e.Key == Key.Enter)
            {
                dayVm.AddTaskCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                dayVm.CancelAddTaskCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    // Auto focus TextBox when adding task
    private void AddTaskTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            // Dispatcher를 사용하여 렌더링 완료 후 포커스
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                textBox.Focus();
                Keyboard.Focus(textBox);
            });
        }
    }

    // Drop on Unscheduled Panel (remove date from task)
    private void UnscheduledPanel_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is Border border)
        {
            // Reset visual state
            border.BorderBrush = null;
            border.BorderThickness = new Thickness(0);

            if (DataContext is WeekViewModel weekVm)
            {
                TaskItemViewModel? taskVm = null;

                // Only accept tasks from day columns (WeekTask)
                if (e.Data.GetDataPresent("WeekTask"))
                {
                    taskVm = e.Data.GetData("WeekTask") as TaskItemViewModel;
                }

                if (taskVm != null)
                {
                    weekVm.RemoveTaskDate(taskVm);
                }
            }
        }

        e.Handled = true;
    }

    private void UnscheduledPanel_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        // Only accept WeekTask (tasks with dates)
        if (e.Data.GetDataPresent("WeekTask"))
        {
            e.Effects = System.Windows.DragDropEffects.Move;

            // Visual feedback
            if (sender is Border border)
            {
                border.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
                border.BorderThickness = new Thickness(2);
            }
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void UnscheduledPanel_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderBrush = null;
            border.BorderThickness = new Thickness(0);
        }
    }

    // Double click to edit task title
    private void TaskTitle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock textBlock && textBlock.DataContext is TaskItemViewModel taskVm)
        {
            var now = DateTime.Now;
            if (_lastClickedItem == taskVm && (now - _lastClickTime).TotalMilliseconds < 500)
            {
                // Double click detected - start editing
                taskVm.StartEditCommand.Execute(null);
                e.Handled = true;
                _lastClickedItem = null;
                _lastClickTime = DateTime.MinValue;
            }
            else
            {
                _lastClickedItem = taskVm;
                _lastClickTime = now;
            }
        }
    }

    // Edit TextBox keyboard handler
    private void EditTaskTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is TaskItemViewModel taskVm)
        {
            if (e.Key == Key.Enter)
            {
                taskVm.SaveEditCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                taskVm.CancelEditCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    // Edit TextBox lost focus handler
    private void EditTaskTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is TaskItemViewModel taskVm)
        {
            if (taskVm.IsEditing)
            {
                taskVm.SaveEditCommand.Execute(null);
            }
        }
    }

    // Auto focus TextBox when editing
    private void EditTaskTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
            {
                textBox.Focus();
                textBox.SelectAll();
                Keyboard.Focus(textBox);
            });
        }
    }

    // Week Search TextBox keyboard handler
    private void WeekSearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            var mainVm = GetMainViewModel();
            mainVm?.CloseSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    // Week QuickAdd TextBox keyboard handler
    private void WeekQuickAddTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        var mainVm = GetMainViewModel();
        if (mainVm == null) return;

        if (e.Key == Key.Enter)
        {
            if (!string.IsNullOrWhiteSpace(mainVm.QuickAddText))
            {
                mainVm.AddTaskCommand.Execute(null);
                Dispatcher.BeginInvoke(() =>
                {
                    WeekQuickAddTextBox.Focus();
                });
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            mainVm.ToggleQuickAddCommand.Execute(null);
            e.Handled = true;
        }
    }

    private MainViewModel? GetMainViewModel()
    {
        var window = Window.GetWindow(this);
        return window?.DataContext as MainViewModel;
    }

    // Public methods for MainWindow to focus input boxes
    public void FocusSearchBox()
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            WeekSearchTextBox.Focus();
            WeekSearchTextBox.SelectAll();
        });
    }

    public void FocusQuickAddBox()
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            WeekQuickAddTextBox.Focus();
            WeekQuickAddTextBox.SelectAll();
        });
    }
}

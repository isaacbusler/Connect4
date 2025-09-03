using System;
using System.Windows.Input;

namespace Connect4.Helpers
{
    /// <summary>
    /// A generic command implementation of <see cref="ICommand"/> that relays its execution logic
    /// to delegates passed in during construction. Supports type-safe parameters and validation.
    /// </summary>
    /// <typeparam name="T">The type of the command parameter.</typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> execute;
        private readonly Predicate<T> canExecute;

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand{T}"/> class.
        /// </summary>
        /// <param name="execute">The action to execute when the command is invoked.</param>
        /// <param name="canExecute">Optional predicate to determine whether the command can execute.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="execute"/> is null.</exception>
        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        /// <summary>
        /// Determines whether the command can execute with the specified parameter.
        /// </summary>
        /// <param name="parameter">The parameter to evaluate.</param>
        /// <returns>True if the command can execute; otherwise, false.</returns>
        public bool CanExecute(object parameter)
        {
            if (canExecute == null)
                return true;

            if (parameter == null)
            {
                if (default(T) == null)
                    return canExecute(default);
                else
                    return false;
            }

            if (parameter is T)
                return canExecute((T)parameter);

            
            if (parameter is string s && typeof(T) == typeof(int) && int.TryParse(s, out int intVal))
            {
                return canExecute((T)(object)intVal);
            }

            return false;
        }

        /// <summary>
        /// Executes the command using the provided parameter.
        /// </summary>
        /// <param name="parameter">The parameter to pass to the execute delegate.</param>
        /// <exception cref="ArgumentNullException">If a non-nullable type is expected but parameter is null.</exception>
        /// <exception cref="InvalidCastException">If the parameter type is invalid.</exception>
        public void Execute(object parameter)
        {
            if (parameter == null)
            {
                if (default(T) == null)
                    execute(default);
                else
                    throw new ArgumentNullException(nameof(parameter), "Command parameter cannot be null.");
            }
            else
            {
                if (parameter is T tParam)
                {
                    execute(tParam);
                }
                else if (parameter is string s && typeof(T) == typeof(int) && int.TryParse(s, out int intVal))
                {
                    execute((T)(object)intVal);
                }
                else
                {
                    throw new InvalidCastException($"Invalid command parameter type. Expected {typeof(T)}, got {parameter.GetType()}");
                }
            }
        }

        /// <summary>
        /// Occurs when changes affect whether the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Raises the <see cref="CanExecuteChanged"/> event to indicate that the result of <see cref="CanExecute"/> may have changed.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// A non-generic command implementation of <see cref="ICommand"/> that relays its execution logic
    /// to delegates passed in during construction. Intended for commands without parameters.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action execute;
        private readonly Func<bool> canExecute;

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand"/> class.
        /// </summary>
        /// <param name="execute">The action to execute when the command is invoked.</param>
        /// <param name="canExecute">Optional function to determine whether the command can execute.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="execute"/> is null.</exception>
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        /// <summary>
        /// Determines whether the command can execute.
        /// </summary>
        /// <param name="parameter">Ignored. This command does not accept parameters.</param>
        /// <returns>True if the command can execute; otherwise, false.</returns>
        public bool CanExecute(object parameter) => canExecute?.Invoke() ?? true;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="parameter">Ignored. This command does not accept parameters.</param>
        public void Execute(object parameter) => execute();

        /// <summary>
        /// Occurs when changes affect whether the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Raises the <see cref="CanExecuteChanged"/> event to indicate that the result of <see cref="CanExecute"/> may have changed.
        /// </summary>
        public void RaiseCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }


}

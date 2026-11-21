using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AmfibiaStudio.Models;
using AmfibiaStudio.Services;
using AmfibiaStudio.ViewModels.Base;

namespace AmfibiaStudio.ViewModels;

/// <summary>Демо-аккаунт сотрудника для быстрого входа.</summary>
public record StaffAccount(string Email, string Name, string Role);

/// <summary>
/// ViewModel входа сотрудника. Поля проверяются сразу при вводе (INotifyDataErrorInfo),
/// кнопка «Войти» включается только когда форма заполнена правильно (CanExecute).
/// </summary>
public class LoginViewModel : ValidatableObject
{
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$");

    private readonly StudioContext context;
    private string email = string.Empty;
    private string password = string.Empty;
    private string errorMessage = string.Empty;

    public LoginViewModel(StudioContext context)
    {
        this.context = context;
        LoginCommand = new RelayCommand(Login, () => Email.Length > 0 && Password.Length > 0 && !HasErrors);
        UseDemoCommand = new RelayCommand(p =>
        {
            if (p is StaffAccount account)
            {
                Email = account.Email;
                Password = "Amfibia2026";
            }
        });

        DemoAccounts = context.State.Users
            .Where(u => u.Role != "buyer" && !u.Blocked)
            .GroupBy(u => u.Role).Select(g => g.First())
            .Select(u => new StaffAccount(u.Email, u.FullName, u.RoleName))
            .ToList();
    }

    public event EventHandler? LoggedIn;

    public string Email
    {
        get => email;
        set
        {
            if (SetProperty(ref email, value.Trim()))
            {
                SetError(email.Length == 0 ? "Введите email." : EmailPattern.IsMatch(email) ? null : "Email указан неверно. Пример: owner@amfibia.by");
                ErrorMessage = string.Empty;
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Password
    {
        get => password;
        set
        {
            if (SetProperty(ref password, value))
            {
                SetError(password.Length == 0 ? "Введите пароль." : null);
                ErrorMessage = string.Empty;
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ErrorMessage
    {
        get => errorMessage;
        private set => SetProperty(ref errorMessage, value);
    }

    public IReadOnlyList<StaffAccount> DemoAccounts { get; }

    public RelayCommand LoginCommand { get; }

    public RelayCommand UseDemoCommand { get; }

    private void Login()
    {
        User? user = context.State.Users.FirstOrDefault(u => string.Equals(u.Email, Email, StringComparison.OrdinalIgnoreCase));
        if (user == null || user.Password != Password)
        {
            ErrorMessage = "Неверный email или пароль.";
            return;
        }

        if (user.Blocked)
        {
            ErrorMessage = "Аккаунт заблокирован.";
            return;
        }

        if (!StudioContext.Sections.ContainsKey(user.Role))
        {
            ErrorMessage = "Панель управления доступна только сотрудникам магазина. Покупателям — приложение AMFIBIA (ЛР №28).";
            return;
        }

        context.CurrentUser = user;
        context.Log("Вход в панель", $"{user.FullName} ({user.RoleName})");
        LoggedIn?.Invoke(this, EventArgs.Empty);
    }
}

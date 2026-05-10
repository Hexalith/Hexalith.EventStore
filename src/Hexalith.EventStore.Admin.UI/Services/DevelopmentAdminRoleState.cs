using Hexalith.EventStore.Admin.Abstractions.Models.Common;

using Microsoft.Extensions.Hosting;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Session-scoped development role override used only when the UI runs in Development
/// without an external authentication authority.
/// </summary>
public sealed class DevelopmentAdminRoleState(IConfiguration configuration, IHostEnvironment environment) {
    private AdminRole _selectedRole = AdminRole.Admin;

    public event Action<AdminRole>? RoleChanged;

    public AdminRole SelectedRole => _selectedRole;

    public bool IsRoleSwitcherAvailable =>
        environment.IsDevelopment()
        && string.IsNullOrWhiteSpace(configuration["EventStore:Authentication:Authority"]);

    public void SetRole(AdminRole role) {
        if (!IsRoleSwitcherAvailable || role == _selectedRole) {
            return;
        }

        _selectedRole = role;
        RoleChanged?.Invoke(role);
    }
}

using Hexalith.EventStore.Admin.Abstractions.Models.Common;

namespace Hexalith.EventStore.Admin.UI.Services;

/// <summary>
/// Session-scoped development role override used only when the UI runs in Development
/// without an external authentication authority.
/// </summary>
public sealed class DevelopmentAdminRoleState(IConfiguration configuration, IHostEnvironment environment) {
    public event Action<AdminRole>? RoleChanged;

    public AdminRole SelectedRole { get; private set; } = AdminRole.Admin;

    public bool IsRoleSwitcherAvailable =>
        environment.IsDevelopment()
        && string.IsNullOrWhiteSpace(configuration["EventStore:Authentication:Authority"]);

    public void SetRole(AdminRole role) {
        if (!IsRoleSwitcherAvailable || role == SelectedRole) {
            return;
        }

        SelectedRole = role;
        RoleChanged?.Invoke(role);
    }
}

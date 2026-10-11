/** Identical to `AuditPermissions` in the backend; granted to tenant admins only. */
export const AUDIT_PERMISSIONS = {
  viewAudit: 'Permissions.Audit.ViewAudit',
  exportAudit: 'Permissions.Audit.ExportAudit',
} as const;

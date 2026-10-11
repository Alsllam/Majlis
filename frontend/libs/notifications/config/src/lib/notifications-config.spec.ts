import { NOTIFICATIONS_PERMISSIONS } from './permissions';

describe('notifications config', () => {
  it('uses the backend permission names', () => {
    expect(NOTIFICATIONS_PERMISSIONS.viewNotification).toBe('Permissions.Notifications.ViewNotification');
  });
});

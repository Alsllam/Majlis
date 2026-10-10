import { Route } from '@angular/router';
import { permissionGuard } from '@majlis/core';
import { KNOWLEDGE_PERMISSIONS } from '@majlis/knowledge-config';

export const KnowledgeUICommonRoutes: Route[] = [
  {
    path: '',
    loadComponent: () => import('./documents/documents-list.component').then((m) => m.DocumentsListComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: KNOWLEDGE_PERMISSIONS.viewDocument },
  },
  {
    path: ':documentId',
    loadComponent: () => import('./document/document.component').then((m) => m.DocumentComponent),
    canActivate: [permissionGuard],
    data: { requiredPolicy: KNOWLEDGE_PERMISSIONS.viewDocument },
  },
];

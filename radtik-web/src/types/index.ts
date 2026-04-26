export type UserRole =
  | 'system_admin'
  | 'employee'
  | 'company_manager'
  | 'client'
  | 'collection_point'

export interface AuthUser {
  id: string
  email: string
  fullName: string
  role: UserRole
}

export const ROLE_ROUTE: Record<UserRole, string> = {
  system_admin: '/admin',
  employee: '/employee',
  company_manager: '/manager',
  client: '/client',
  collection_point: '/collection',
}

export const ROLE_LABEL_KEY: Record<UserRole, string> = {
  system_admin: 'roles.systemAdmin',
  employee: 'roles.employee',
  company_manager: 'roles.companyManager',
  client: 'roles.client',
  collection_point: 'roles.collectionPoint',
}

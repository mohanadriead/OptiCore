import { useQuery } from '@tanstack/react-query'
import { useAuth } from '../auth/authContext'
import { getEffectivePermissions, permissionCodes, permissionKeys, type PermissionCode } from './permissionApi'

// UI convenience only: future backend use cases must independently authorize each action.
export function usePermissions() {
  const { status, employee } = useAuth()
  const authenticated = status === 'authenticated' && !!employee?.isActive
  const query = useQuery({
    queryKey: [...permissionKeys.effective, employee?.id, employee?.isManager],
    queryFn: ({ signal }) => getEffectivePermissions(signal),
    enabled: authenticated,
    staleTime: 0,
    refetchOnWindowFocus: 'always',
  })
  return {
    hasPermission: (code: PermissionCode): boolean => authenticated && permissionCodes.includes(code) &&
      (!!employee?.isManager || (query.isSuccess && query.data.includes(code))),
    isPending: authenticated && !employee?.isManager && query.isPending,
    error: query.error,
    refresh: query.refetch,
  }
}

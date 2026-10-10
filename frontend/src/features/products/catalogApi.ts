import { apiRequest } from '@/lib/apiClient'

export const categories = { Frames: 'מסגרות', Sunglasses: 'משקפי שמש', Lenses: 'עדשות', ContactLenses: 'עדשות מגע', Accessories: 'אביזרים', CleaningProducts: 'מוצרי ניקוי', Cases: 'נרתיקים', Other: 'אחר' } as const
export const genders = { Men: 'גברים', Women: 'נשים', Unisex: 'יוניסקס', Kids: 'ילדים' } as const
export type Category = keyof typeof categories
export type Gender = keyof typeof genders
export interface Attribute { key: string; value: string }
export interface ProductInput {
  category: Category; barcode: string | null; brandId: string | null; model: string | null; color: string | null; size: string | null
  genderCategory: Gender | null; lensType: string | null; regularSalePrice: number; promoPrice: number | null; attributes: Attribute[]
}
interface Audit { createdAtUtc: string; createdByEmployeeId: string; updatedAtUtc: string | null; updatedByEmployeeId: string | null }
export interface Product extends ProductInput, Audit { id: string; productNumber: number; isActive: boolean; brandName: string | null; brandIsActive: boolean | null }
export interface Brand extends Audit { id: string; name: string; isActive: boolean }
export interface ProductPage { items: Product[]; total: number; page: number; pageSize: number }
export interface Filters { q: string; category: string; brandId: string; status: string; page: number }
const body = (method: string, data?: unknown): RequestInit => ({ method, headers: { 'Content-Type': 'application/json' }, body: data === undefined ? undefined : JSON.stringify(data) })
export const listProducts = (filters: Filters, signal?: AbortSignal) => {
  const query = new URLSearchParams({ status: filters.status, page: String(filters.page), pageSize: '50' })
  for (const key of ['q', 'category', 'brandId'] as const) if (filters[key]) query.set(key, filters[key])
  return apiRequest<ProductPage>(`/api/products?${query}`, { signal })
}
export const getProduct = (number: string, signal?: AbortSignal) => apiRequest<Product>(`/api/products/${number}`, { signal })
export const saveProduct = (data: ProductInput, number?: number) => apiRequest<Product>(number ? `/api/products/${number}` : '/api/products', body(number ? 'PUT' : 'POST', data))
export const setProductActive = (product: Product) => apiRequest<Product>(`/api/products/${product.productNumber}/${product.isActive ? 'deactivate' : 'activate'}`, body('PATCH'))
export const listBrands = (signal?: AbortSignal) => apiRequest<Brand[]>('/api/brands?includeInactive=true', { signal })
export const saveBrand = (name: string, id?: string) => apiRequest<Brand>(id ? `/api/brands/${id}` : '/api/brands', body(id ? 'PUT' : 'POST', { name: name.trim() }))
export const setBrandActive = (brand: Brand) => apiRequest<Brand>(`/api/brands/${brand.id}/${brand.isActive ? 'deactivate' : 'activate'}`, body('PATCH'))
export const price = (value: number) => new Intl.NumberFormat('he-IL', { style: 'currency', currency: 'ILS' }).format(value)

import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { errorMessage } from '@/lib/apiClient'
import { getProduct, listBrands, saveProduct } from './catalogApi'
import { ProductForm } from './ProductForm'

export function ProductEditorPage() {
  const { productNumber } = useParams()
  const navigate = useNavigate()
  const client = useQueryClient()
  const brands = useQuery({ queryKey: ['brands'], queryFn: ({ signal }) => listBrands(signal) })
  const product = useQuery({ queryKey: ['products', productNumber], queryFn: ({ signal }) => getProduct(productNumber!, signal), enabled: !!productNumber })
  if (brands.isError || (productNumber && product.isError)) return <div><p role="alert">{errorMessage(brands.error ?? product.error)}</p><Button onClick={() => { void brands.refetch(); if (productNumber) void product.refetch() }}>ניסיון נוסף</Button></div>
  if (!brands.data || (productNumber && !product.data)) return <p role="status">טוען פרטי מוצר…</p>
  return <div className="space-y-6"><h1>{productNumber ? `עריכת מוצר ${productNumber}` : 'מוצר חדש'}</h1><ProductForm key={productNumber ?? 'new'} brands={brands.data} product={product.data} onSave={async input => {
    const saved = await saveProduct(input, product.data?.productNumber)
    await client.invalidateQueries({ queryKey: ['products'] })
    navigate(`/products/${saved.productNumber}`)
  }} /></div>
}

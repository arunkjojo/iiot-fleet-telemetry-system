declare module 'leaflet.markercluster/dist/MarkerCluster.css'
declare module 'leaflet.markercluster/dist/MarkerCluster.Default.css'

declare module '@changey/react-leaflet-markercluster' {
  import type { PropsWithChildren } from 'react'
  import type L from 'leaflet'

  export interface MarkerClusterGroupProps {
    chunkedLoading?: boolean
    disableClusteringAtZoom?: number
    spiderfyOnMaxZoom?: boolean
    showCoverageOnHover?: boolean
    zoomToBoundsOnClick?: boolean
    maxClusterRadius?: number
    iconCreateFunction?: (cluster: L.MarkerCluster) => L.Icon | L.DivIcon
  }

  const MarkerClusterGroup: React.FC<PropsWithChildren<MarkerClusterGroupProps>>
  export default MarkerClusterGroup
}

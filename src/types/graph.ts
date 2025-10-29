export interface GraphNode {
  id: string;
  label: string;
  type: string;
  size: number;
  color: string;
  x?: number;
  y?: number;
  vx?: number;
  vy?: number;
  fx?: number | null;
  fy?: number | null;
}

export interface GraphLink {
  source: string | GraphNode;
  target: string | GraphNode;
  strength: number;
  type: string;
  color: string;
  width: number;
}

export interface GraphData {
  nodes: GraphNode[];
  links: GraphLink[];
}

export enum LayoutType {
  FORCE_DIRECTED = "force-directed",
  HIERARCHICAL = "hierarchical",
  CIRCULAR = "circular",
  RADIAL = "radial"
}

export interface GraphSettings {
  layout: LayoutType;
  physicsEnabled: boolean;
  physicsStrength: number;
  nodeSizeBy: "connections" | "importance";
  showLabels: boolean;
  showEdgeLabels: boolean;
  minEdgeStrength: number;
  animationSpeed: number;
}

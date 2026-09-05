export type SectionType = 'powered' | 'normal' | 'station';

export interface TrainConfiguration {
  mass: number;
  maximumForce: number;
  precision: number;
  initialSpeed: number;
}

export interface SectionConfiguration {
  type: SectionType;
  distance?: number;
  force?: number;
  alightingTime?: number;
  boardingTime?: number;
  speedLimit?: number;
}

export interface RouteConfiguration {
  train: TrainConfiguration;
  endSpeedLimit: number;
  sections: SectionConfiguration[];
}

export interface SimulationReport {
  succeeded: boolean;
  sectionCount: number;
  finalSpeed: number | null;
  elapsedTime: number | null;
  completedSectionsElapsedTime: number;
  summary: string;
  failedSection: number | null;
}

export interface SimulationMetrics {
  totalElapsedTime: number | null;
  movingTime: number;
  configuredStationWait: number;
  executedStationWait: number | null;
  plannedTrackDistance: number;
  actualTrackDistance: number | null;
  averageSampledSpeed: number;
  maximumSampledSpeed: number;
  minimumSampledSpeed: number;
  maximumModeledAcceleration: number | null;
  smallestSpeedLimitMargin: number | null;
  sectionCount: number;
  stationCount: number;
  tightestConstraint: string;
}

export interface SectionAnalysis {
  index: number;
  type: string;
  succeeded: boolean;
  entrySpeed: number;
  exitSpeed: number | null;
  elapsedTime: number | null;
  plannedDistance: number | null;
  configuredStationWait: number;
  executedStationWait: number | null;
  modeledPeakAcceleration: number | null;
  speedLimit: number | null;
  speedLimitMargin: number | null;
  result: string;
}

export interface SimulationAnalysis {
  report: SimulationReport;
  metrics: SimulationMetrics;
  trace: SectionAnalysis[];
}

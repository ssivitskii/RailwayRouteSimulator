import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, Observable, throwError } from 'rxjs';
import { RouteConfiguration, SimulationAnalysis } from './simulation.models';

interface ProblemDetails {
  title?: unknown;
  detail?: unknown;
  errors?: Record<string, unknown>;
}

export function extractProblemDetail(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return error instanceof Error ? error.message : 'The simulation request failed.';
  }

  const problem = error.error as ProblemDetails | string | null;
  if (typeof problem === 'string' && problem.trim()) return problem;
  if (problem && typeof problem === 'object') {
    if (typeof problem.detail === 'string' && problem.detail.trim()) return problem.detail;
    if (problem.errors && typeof problem.errors === 'object') {
      const first = Object.values(problem.errors)
        .flat()
        .find((value) => typeof value === 'string');
      if (typeof first === 'string') return first;
    }
    if (typeof problem.title === 'string' && problem.title.trim()) return problem.title;
  }

  return error.status === 0
    ? 'The simulator API is unreachable. Start it on port 8080 and try again.'
    : `The simulator returned HTTP ${error.status}.`;
}

@Injectable({ providedIn: 'root' })
export class SimulationApiService {
  private readonly http = inject(HttpClient);

  analyze(configuration: RouteConfiguration): Observable<SimulationAnalysis> {
    return this.http
      .post<SimulationAnalysis>('/api/simulations/analyze', configuration)
      .pipe(
        catchError((error: unknown) => throwError(() => new Error(extractProblemDetail(error)))),
      );
  }
}

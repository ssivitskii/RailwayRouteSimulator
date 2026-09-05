import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { SimulationApiService } from './simulation-api.service';
import { RouteConfiguration } from './simulation.models';

describe('SimulationApiService', () => {
  let service: SimulationApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(SimulationApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the exact route configuration to the analyze endpoint', () => {
    const configuration: RouteConfiguration = {
      train: { mass: 1000, maximumForce: 2000, precision: 0.1, initialSpeed: 0 },
      endSpeedLimit: 6,
      sections: [{ type: 'normal', distance: 10 }],
    };

    service.analyze(configuration).subscribe();
    const request = http.expectOne('/api/simulations/analyze');

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(configuration);
    request.flush({ report: {}, metrics: {}, trace: [] });
  });

  it('surfaces ProblemDetails detail text', async () => {
    const configuration: RouteConfiguration = {
      train: { mass: 1000, maximumForce: 2000, precision: 0.1, initialSpeed: 0 },
      endSpeedLimit: 6,
      sections: [{ type: 'normal', distance: 10 }],
    };
    const result = firstValueFrom(service.analyze(configuration));
    http.expectOne('/api/simulations/analyze').flush(
      {
        title: 'Invalid route configuration',
        detail: 'Precision is outside the safety envelope.',
      },
      { status: 400, statusText: 'Bad Request' },
    );

    await expect(result).rejects.toThrow('Precision is outside the safety envelope.');
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { App } from './app';
import { SimulationApiService } from './simulation-api.service';
import { RouteConfiguration, SimulationAnalysis } from './simulation.models';

class SimulationApiStub {
  readonly requests: RouteConfiguration[] = [];
  readonly responses: Subject<SimulationAnalysis>[] = [];

  analyze(configuration: RouteConfiguration): Subject<SimulationAnalysis> {
    const response = new Subject<SimulationAnalysis>();
    this.requests.push(configuration);
    this.responses.push(response);
    return response;
  }
}

describe('App', () => {
  let fixture: ComponentFixture<App>;
  let component: App;
  let api: SimulationApiStub;

  beforeEach(async () => {
    api = new SimulationApiStub();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [{ provide: SimulationApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(App);
    component = fixture.componentInstance;
  });

  it('loads presets and supports add, move, and remove editing', () => {
    expect(component.sectionControls.length).toBe(4);

    component.applyPreset(0);
    component.addSection('station');
    component.moveSection(2, -1);

    expect(component.sectionControls.length).toBe(3);
    expect(component.sectionControls[1].controls.type.value).toBe('station');
    component.removeSection(1);
    expect(component.sectionControls.map((section) => section.controls.type.value)).toEqual([
      'powered',
      'normal',
    ]);
  });

  it('renders server metrics, route segments, and trace after a successful response', () => {
    component.runSimulation();
    api.responses[0].next(createAnalysis('Route completed successfully.'));
    fixture.detectChanges();

    expect(component.state()).toBe('success');
    expect(component.analysis()?.metrics.plannedTrackDistance).toBe(40);
    expect(fixture.nativeElement.textContent).toContain('Route completed successfully.');
    expect(fixture.nativeElement.querySelectorAll('.segment').length).toBe(4);
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(4);
  });

  it('ignores an earlier response after a newer run starts', () => {
    component.runSimulation();
    component.runSimulation();

    api.responses[0].next(createAnalysis('stale result'));
    api.responses[1].next(createAnalysis('latest result'));

    expect(component.analysis()?.report.summary).toBe('latest result');
  });

  it('holds the train marker at the station point during station wait', () => {
    component.runSimulation();
    api.responses[0].next(createAnalysis('success'));

    component.playbackSeconds.set(25);

    expect(component.playbackProgress()).toBeCloseTo((36 / 52) * 100, 5);
  });

  it('explains a negative final speed limit and does not submit it', () => {
    component.form.controls.endSpeedLimit.setValue(-1);

    component.runSimulation();
    fixture.detectChanges();

    expect(api.requests.length).toBe(0);
    expect(fixture.nativeElement.textContent).toContain(
      'final speed limit must be zero or greater',
    );
  });
});

function createAnalysis(summary: string): SimulationAnalysis {
  return {
    report: {
      succeeded: true,
      sectionCount: 4,
      finalSpeed: 3.2,
      elapsedTime: 42,
      completedSectionsElapsedTime: 42,
      summary,
      failedSection: null,
    },
    metrics: {
      totalElapsedTime: 42,
      movingTime: 33,
      configuredStationWait: 9,
      executedStationWait: 9,
      plannedTrackDistance: 40,
      actualTrackDistance: 40,
      averageSampledSpeed: 2.1,
      maximumSampledSpeed: 3.2,
      minimumSampledSpeed: 0,
      maximumModeledAcceleration: 0.5,
      smallestSpeedLimitMargin: 2.8,
      sectionCount: 4,
      stationCount: 1,
      tightestConstraint: 'Final speed limit',
    },
    trace: [
      createTrace(0, 'PoweredTrack', 0, 3),
      createTrace(1, 'NormalTrack', 3, 3),
      createTrace(2, 'Station', 3, 3),
      createTrace(3, 'NormalTrack', 3, 3.2),
    ],
  };
}

function createTrace(index: number, type: string, entrySpeed: number, exitSpeed: number) {
  return {
    index,
    type,
    succeeded: true,
    entrySpeed,
    exitSpeed,
    elapsedTime: 10,
    plannedDistance: type === 'Station' ? null : 10,
    configuredStationWait: type === 'Station' ? 9 : 0,
    executedStationWait: type === 'Station' ? 9 : 0,
    modeledPeakAcceleration: type === 'PoweredTrack' ? 0.5 : null,
    speedLimit: type === 'Station' ? 6 : null,
    speedLimitMargin: type === 'Station' ? 3 : null,
    result: 'Success',
  };
}

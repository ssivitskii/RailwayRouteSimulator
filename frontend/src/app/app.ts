import { CommonModule } from '@angular/common';
import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Subscription } from 'rxjs';
import { SimulationApiService } from './simulation-api.service';
import {
  RouteConfiguration,
  SectionConfiguration,
  SectionType,
  SimulationAnalysis,
} from './simulation.models';

type RunState = 'idle' | 'loading' | 'success' | 'failure';

interface SectionControls {
  type: FormControl<SectionType>;
  distance: FormControl<number | null>;
  force: FormControl<number | null>;
  alightingTime: FormControl<number | null>;
  boardingTime: FormControl<number | null>;
  speedLimit: FormControl<number | null>;
}

interface RouteFormControls {
  mass: FormControl<number>;
  maximumForce: FormControl<number>;
  precision: FormControl<number>;
  initialSpeed: FormControl<number>;
  endSpeedLimit: FormControl<number>;
  sections: FormArray<FormGroup<SectionControls>>;
}

interface Preset {
  name: string;
  description: string;
  configuration: RouteConfiguration;
}

const MAXIMUM_SECTIONS = 64;
const PLAYBACK_RATE = 8;

const PRESETS: readonly Preset[] = [
  {
    name: 'Starter line',
    description: 'Powered acceleration followed by a short coast.',
    configuration: {
      train: { mass: 1000, maximumForce: 2000, precision: 0.1, initialSpeed: 0 },
      endSpeedLimit: 6,
      sections: [
        { type: 'powered', distance: 20, force: 500 },
        { type: 'normal', distance: 10 },
      ],
    },
  },
  {
    name: 'Station service',
    description: 'Acceleration, a scheduled stop, then the final approach.',
    configuration: {
      train: { mass: 1000, maximumForce: 2000, precision: 0.1, initialSpeed: 0 },
      endSpeedLimit: 6,
      sections: [
        { type: 'powered', distance: 20, force: 500 },
        { type: 'normal', distance: 10 },
        { type: 'station', alightingTime: 5, boardingTime: 4, speedLimit: 6 },
        { type: 'normal', distance: 10 },
      ],
    },
  },
];

@Component({
  selector: 'app-root',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnDestroy {
  private readonly api = inject(SimulationApiService);
  private requestGeneration = 0;
  private requestSubscription?: Subscription;
  private animationFrame?: number;
  private previousFrameTime?: number;

  readonly presets = PRESETS;
  readonly maximumSections = MAXIMUM_SECTIONS;
  readonly state = signal<RunState>('idle');
  readonly analysis = signal<SimulationAnalysis | null>(null);
  readonly errorMessage = signal('');
  readonly lastRunConfiguration = signal<RouteConfiguration | null>(null);
  readonly isPlaying = signal(false);
  readonly playbackSeconds = signal(0);
  readonly playbackDuration = computed(() => {
    const result = this.analysis();
    if (!result) return 0;
    return (
      result.report.elapsedTime ??
      result.trace.reduce((total, section) => total + (section.elapsedTime ?? 0), 0)
    );
  });
  readonly playbackProgress = computed(() => {
    const result = this.analysis();
    const configuration = this.lastRunConfiguration();
    if (!result || !configuration || result.trace.length === 0) return 0;

    const weights = configuration.sections.map((section) => this.segmentWeight(section));
    const totalWeight = weights.reduce((total, weight) => total + weight, 0);
    let elapsed = 0;
    let traversedWeight = 0;
    for (let index = 0; index < result.trace.length; index++) {
      const section = result.trace[index];
      const sectionDuration = section.elapsedTime ?? 0;
      const weight = weights[index] ?? 0;
      if (this.playbackSeconds() <= elapsed + sectionDuration) {
        if (configuration.sections[index]?.type === 'station') {
          return ((traversedWeight + weight / 2) / totalWeight) * 100;
        }
        const ratio =
          sectionDuration > 0 ? (this.playbackSeconds() - elapsed) / sectionDuration : 1;
        return ((traversedWeight + Math.max(0, ratio) * weight) / totalWeight) * 100;
      }
      elapsed += sectionDuration;
      traversedWeight += weight;
    }

    return 100;
  });

  readonly form = new FormGroup<RouteFormControls>({
    mass: new FormControl(1000, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.001)],
    }),
    maximumForce: new FormControl(2000, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)],
    }),
    precision: new FormControl(0.1, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0.001)],
    }),
    initialSpeed: new FormControl(0, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)],
    }),
    endSpeedLimit: new FormControl(6, {
      nonNullable: true,
      validators: [Validators.required, Validators.min(0)],
    }),
    sections: new FormArray<FormGroup<SectionControls>>([]),
  });

  constructor() {
    this.applyPreset(1);
  }

  get sectionControls(): FormGroup<SectionControls>[] {
    return this.form.controls.sections.controls;
  }

  applyPreset(index: number): void {
    const preset = this.presets[index];
    if (!preset) return;
    const configuration = structuredClone(preset.configuration);
    this.form.patchValue({
      mass: configuration.train.mass,
      maximumForce: configuration.train.maximumForce,
      precision: configuration.train.precision,
      initialSpeed: configuration.train.initialSpeed,
      endSpeedLimit: configuration.endSpeedLimit,
    });
    this.form.controls.sections.clear();
    configuration.sections.forEach((section) =>
      this.form.controls.sections.push(this.createSection(section)),
    );
    this.form.markAsPristine();
    this.clearResult();
  }

  addSection(type: SectionType = 'normal'): void {
    if (this.sectionControls.length >= MAXIMUM_SECTIONS) return;
    const defaults: Record<SectionType, SectionConfiguration> = {
      powered: { type: 'powered', distance: 10, force: 500 },
      normal: { type: 'normal', distance: 10 },
      station: { type: 'station', alightingTime: 3, boardingTime: 3, speedLimit: 6 },
    };
    this.form.controls.sections.push(this.createSection(defaults[type]));
    this.clearResult();
  }

  removeSection(index: number): void {
    if (this.sectionControls.length <= 1) return;
    this.form.controls.sections.removeAt(index);
    this.clearResult();
  }

  moveSection(index: number, direction: -1 | 1): void {
    const destination = index + direction;
    if (destination < 0 || destination >= this.sectionControls.length) return;
    const control = this.form.controls.sections.at(index);
    this.form.controls.sections.removeAt(index);
    this.form.controls.sections.insert(destination, control);
    this.clearResult();
  }

  sectionTypeChanged(section: FormGroup<SectionControls>): void {
    section.updateValueAndValidity();
    this.clearResult();
  }

  runSimulation(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.sectionControls.length === 0) return;

    const configuration = this.toConfiguration();
    const generation = ++this.requestGeneration;
    this.requestSubscription?.unsubscribe();
    this.pause();
    this.analysis.set(null);
    this.lastRunConfiguration.set(null);
    this.errorMessage.set('');
    this.state.set('loading');

    this.requestSubscription = this.api.analyze(configuration).subscribe({
      next: (analysis) => {
        if (generation !== this.requestGeneration) return;
        this.analysis.set(analysis);
        this.lastRunConfiguration.set(configuration);
        this.state.set(analysis.report.succeeded ? 'success' : 'failure');
        this.playbackSeconds.set(0);
      },
      error: (error: unknown) => {
        if (generation !== this.requestGeneration) return;
        this.errorMessage.set(
          error instanceof Error ? error.message : 'The simulation request failed.',
        );
        this.state.set('failure');
      },
    });
  }

  play(): void {
    const duration = this.playbackDuration();
    if (duration <= 0 || this.isPlaying()) return;
    if (this.playbackSeconds() >= duration) this.playbackSeconds.set(0);
    this.isPlaying.set(true);
    this.previousFrameTime = undefined;
    this.animationFrame = requestAnimationFrame((time) => this.advancePlayback(time));
  }

  pause(): void {
    this.isPlaying.set(false);
    this.previousFrameTime = undefined;
    if (this.animationFrame !== undefined) cancelAnimationFrame(this.animationFrame);
    this.animationFrame = undefined;
  }

  resetPlayback(): void {
    this.pause();
    this.playbackSeconds.set(0);
  }

  scrub(event: Event): void {
    this.pause();
    const input = event.target as HTMLInputElement;
    this.playbackSeconds.set(Number(input.value));
  }

  speedProfilePoints(): string {
    const trace = this.analysis()?.trace ?? [];
    if (trace.length === 0) return '';
    const samples = [
      trace[0].entrySpeed,
      ...trace.map((section) => section.exitSpeed ?? section.entrySpeed),
    ];
    const maximum = Math.max(1, ...samples);
    return samples
      .map((speed, index) => {
        const x = 8 + (index / Math.max(1, samples.length - 1)) * 84;
        const y = 88 - (speed / maximum) * 68;
        return `${x.toFixed(2)},${y.toFixed(2)}`;
      })
      .join(' ');
  }

  segmentWeight(section: SectionConfiguration): number {
    return section.type === 'station' ? 12 : Math.max(8, section.distance ?? 0);
  }

  format(value: number | null | undefined, digits = 2): string {
    return value == null ? '—' : value.toFixed(digits);
  }

  ngOnDestroy(): void {
    this.requestSubscription?.unsubscribe();
    this.pause();
  }

  private advancePlayback(timestamp: number): void {
    if (!this.isPlaying()) return;
    if (this.previousFrameTime !== undefined) {
      const next =
        this.playbackSeconds() + ((timestamp - this.previousFrameTime) / 1000) * PLAYBACK_RATE;
      const duration = this.playbackDuration();
      this.playbackSeconds.set(Math.min(duration, next));
      if (next >= duration) {
        this.pause();
        return;
      }
    }
    this.previousFrameTime = timestamp;
    this.animationFrame = requestAnimationFrame((time) => this.advancePlayback(time));
  }

  private clearResult(): void {
    this.requestGeneration++;
    this.requestSubscription?.unsubscribe();
    this.pause();
    this.state.set('idle');
    this.analysis.set(null);
    this.lastRunConfiguration.set(null);
    this.errorMessage.set('');
    this.playbackSeconds.set(0);
  }

  private createSection(section: SectionConfiguration): FormGroup<SectionControls> {
    return new FormGroup<SectionControls>(
      {
        type: new FormControl(section.type, { nonNullable: true }),
        distance: new FormControl(section.distance ?? null),
        force: new FormControl(section.force ?? null),
        alightingTime: new FormControl(section.alightingTime ?? null),
        boardingTime: new FormControl(section.boardingTime ?? null),
        speedLimit: new FormControl(section.speedLimit ?? null),
      },
      { validators: [this.validateSection] },
    );
  }

  private readonly validateSection = (control: AbstractControl): ValidationErrors | null => {
    const section = control.value as SectionConfiguration;
    const positive = (value: unknown): boolean =>
      typeof value === 'number' && Number.isFinite(value) && value > 0;
    const nonNegative = (value: unknown): boolean =>
      typeof value === 'number' && Number.isFinite(value) && value >= 0;
    if (section.type === 'powered') {
      return positive(section.distance) &&
        typeof section.force === 'number' &&
        Number.isFinite(section.force)
        ? null
        : { section: true };
    }
    if (section.type === 'normal') return positive(section.distance) ? null : { section: true };
    return nonNegative(section.alightingTime) &&
      nonNegative(section.boardingTime) &&
      nonNegative(section.speedLimit)
      ? null
      : { section: true };
  };

  private toConfiguration(): RouteConfiguration {
    const raw = this.form.getRawValue();
    return {
      train: {
        mass: raw.mass,
        maximumForce: raw.maximumForce,
        precision: raw.precision,
        initialSpeed: raw.initialSpeed,
      },
      endSpeedLimit: raw.endSpeedLimit,
      sections: raw.sections.map((section) => {
        if (section.type === 'station') {
          return {
            type: section.type,
            alightingTime: section.alightingTime!,
            boardingTime: section.boardingTime!,
            speedLimit: section.speedLimit!,
          };
        }
        return {
          type: section.type,
          distance: section.distance!,
          ...(section.type === 'powered' ? { force: section.force! } : {}),
        };
      }),
    };
  }
}

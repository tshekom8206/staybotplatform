import { AfterViewInit, Directive, ElementRef, Input, OnChanges, SimpleChanges } from '@angular/core';
import * as feather from 'feather-icons';

@Directive({
  selector: '[appFeatherIcon]',
  standalone: true
})
export class FeatherIconDirective implements AfterViewInit, OnChanges {
  @Input() appFeatherIcon!: string;

  constructor(private elementRef: ElementRef) { }

  ngAfterViewInit(): void {
    this.updateIcon();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['appFeatherIcon']) {
      this.updateIcon();
    }
  }

  private updateIcon(): void {
    const element = this.elementRef.nativeElement;
    if (this.appFeatherIcon) {
      element.setAttribute('data-feather', this.appFeatherIcon);
      feather.replace();
    }
  }
}

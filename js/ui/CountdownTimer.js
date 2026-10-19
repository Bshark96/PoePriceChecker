// Countdown Timer Constructor
function CountdownTimer(totalSeconds, timerTextId, progressBarId, wrapperId) {
  this.totalDuration = totalSeconds || 5.0;
  this.remainingTime = this.totalDuration;
  this.isHovered = false;
  this.timerTextEl = document.getElementById(timerTextId);
  this.progressBarEl = document.getElementById(progressBarId);
  this.wrapperEl = document.getElementById(wrapperId);
  this.intervalId = null;

  this.initHoverEvents();
}

CountdownTimer.prototype.initHoverEvents = function() {
  var self = this;
  if (!this.wrapperEl) return;
  if (window.PoeBridge && window.PoeBridge.isGameBarHost()) {
    return;
  }

  this.wrapperEl.addEventListener('mouseenter', function() {
    self.isHovered = true;
    var autoTextSpan = document.getElementById('autoMinimizeText');
    if (autoTextSpan) {
      autoTextSpan.innerHTML = 'Auto-minimizing in ' + self.remainingTime.toFixed(1) + 's (Hovered - Paused)';
    }
  });

  this.wrapperEl.addEventListener('mouseleave', function() {
    self.isHovered = false;
    var autoTextSpan = document.getElementById('autoMinimizeText');
    if (autoTextSpan) {
      autoTextSpan.innerHTML = 'Auto-minimizing in <span id="autoMinimizeTimer">' + self.remainingTime.toFixed(1) + '</span>s (Hover to pause)';
    }
  });
};

CountdownTimer.prototype.start = function() {
  var self = this;
  if (this.intervalId) clearInterval(this.intervalId);

  // When running inside C# GameBar host, C# sends precise onTimerTick events
  if (window.PoeBridge && window.PoeBridge.isGameBarHost()) {
    return;
  }

  this.intervalId = setInterval(function() {
    if (!self.isHovered && self.remainingTime > 0) {
      self.remainingTime -= 0.1;
      if (self.remainingTime < 0) self.remainingTime = 0;

      var timerEl = document.getElementById('autoMinimizeTimer');
      if (timerEl) {
        timerEl.innerText = self.remainingTime.toFixed(1);
      }

      if (self.progressBarEl) {
        var pct = (self.remainingTime / self.totalDuration) * 100;
        self.progressBarEl.style.width = pct + '%';
      }

      if (self.remainingTime <= 0) {
        var autoTextSpan = document.getElementById('autoMinimizeText');
        if (autoTextSpan) {
          autoTextSpan.innerHTML = 'Auto-minimizing in 0.0s (Hover to pause)';
        }
      }
    }
  }, 100);
};

CountdownTimer.prototype.reset = function() {
  this.remainingTime = this.totalDuration;
  if (this.progressBarEl) {
    this.progressBarEl.style.width = '100%';
  }
};

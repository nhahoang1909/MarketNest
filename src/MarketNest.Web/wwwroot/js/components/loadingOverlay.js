/**
 * Alpine.js store — loading overlay controller.
 *
 * Usage:
 *   Alpine.store('loading').show()
 *   Alpine.store('loading').show('Placing order…', 'Verifying payment and reserving items.')
 *   Alpine.store('loading').hide()
 */
document.addEventListener('alpine:init', () => {
  const SKETCH_KEYS = ['bag', 'truck', 'box', 'store', 'shopper', 'plane', 'plant', 'receipt'];

  const COPY = {
    bag:     ['Hang tight\u2026',         'Filling your bag with fresh picks from local sellers.'],
    truck:   ['On the way\u2026',         'Routing the fastest delivery from nearby warehouses.'],
    box:     ['Almost packed\u2026',      'Verifying inventory and quality checks on your order.'],
    store:   ['Opening the market\u2026', 'Loading storefronts and today\u2019s featured shops.'],
    shopper: ['Just a moment\u2026',      'Personalising recommendations for you.'],
    plane:   ['Sending it\u2026',         'Dispatching to fulfilment \u2014 tracking link incoming.'],
    plant:   ['Sourcing fresh\u2026',     'Pulling in organic and sustainable listings.'],
    receipt: ['Securing checkout\u2026',  'Encrypting payment and confirming the receipt.'],
  };

  const CYCLE_MS = 2400;

  Alpine.store('loading', {
    visible: false,
    keys: SKETCH_KEYS,
    activeIndex: 0,
    activeKey: SKETCH_KEYS[0],
    title: COPY[SKETCH_KEYS[0]][0],
    subtitle: COPY[SKETCH_KEYS[0]][1],
    _timer: null,
    _customTitle: null,
    _customSub: null,

    show(customTitle, customSub) {
      this._customTitle = customTitle || null;
      this._customSub = customSub || null;
      this.activeIndex = 0;
      this._applyActive(0);
      this.visible = true;
      this._startCycle();
    },

    hide() {
      this.visible = false;
      this._stopCycle();
      this._customTitle = null;
      this._customSub = null;
    },

    _applyActive(idx) {
      this.activeIndex = idx;
      this.activeKey = SKETCH_KEYS[idx];
      if (this._customTitle) {
        this.title = this._customTitle;
        this.subtitle = this._customSub || '';
      } else {
        const key = SKETCH_KEYS[idx];
        this.title = COPY[key][0];
        this.subtitle = COPY[key][1];
      }
    },

    _startCycle() {
      this._stopCycle();
      this._timer = setInterval(() => {
        const next = (this.activeIndex + 1) % SKETCH_KEYS.length;
        this._applyActive(next);
      }, CYCLE_MS);
    },

    _stopCycle() {
      if (this._timer) {
        clearInterval(this._timer);
        this._timer = null;
      }
    },
  });
});


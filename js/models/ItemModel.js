// Path of Exile 1 Item Domain Model Constructor
function ItemModel(options) {
  options = options || {};
  this.name = options.name || 'Unknown Item';
  this.base = options.base || '';
  this.category = options.category || 'General';
  this.rarity = options.rarity || 'Rare';
  this.rarityKey = options.rarityKey || 'rare';
  this.itemLevel = options.itemLevel || 84;
  this.isCorrupted = !!options.isCorrupted;
  this.corruptedFilterActive = !!options.corruptedFilterActive;
  this.quality = options.quality || 20;
  this.properties = options.properties || [];
  this.modifiers = options.modifiers || [];
  this.ninjaPrice = options.ninjaPrice || '';
}

ItemModel.prototype.getDisplayName = function() {
  if (this.name && this.base && this.name !== this.base) {
    return this.name + ' (' + this.base + ')';
  }
  return this.name || this.base || 'Unknown Item';
};

ItemModel.prototype.isGem = function() {
  return this.rarityKey === 'gem' || this.rarity.toLowerCase() === 'gem';
};

ItemModel.prototype.isUnique = function() {
  return this.rarityKey === 'doryani' || this.rarity.toLowerCase() === 'unique';
};

ItemModel.prototype.isDivinationCard = function() {
  return this.rarityKey === 'chambers' || (this.category && this.category.toLowerCase().indexOf('card') >= 0);
};

ItemModel.prototype.toJSON = function() {
  return {
    name: this.name,
    base: this.base,
    category: this.category,
    rarity: this.rarity,
    rarityKey: this.rarityKey,
    itemLevel: this.itemLevel,
    isCorrupted: this.isCorrupted,
    properties: this.properties,
    modifiers: this.modifiers,
    ninjaPrice: this.ninjaPrice
  };
};

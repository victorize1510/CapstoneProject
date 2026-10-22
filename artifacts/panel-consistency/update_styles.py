"""One-off reviewed edits to owning USS rules; preserve cascade order."""
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'ui-style-audit'))
from style_tools import rules, render, prune

UI = Path(__file__).resolve().parents[2] / 'Capstone/Assets/ThangTest/UIThang/Inventory/UI'
BG = 'var(--menu-background, #f8f4eb)'
WHITE = 'var(--menu-surface, #ffffff)'

def edit(name, changes):
    path = UI / (name + '.uss')
    items = rules(path.read_text(encoding='utf-8-sig'))
    for selector, values in changes.items():
        # Split grouped rules so a change cannot recolor unrelated controls.
        expanded = []
        for selectors, declarations in items:
            expanded.extend(([s], declarations.copy()) for s in selectors)
        items = expanded
        indices = [i for i, (s, _) in enumerate(items) if s == [selector]]
        if not indices:
            items.append(([selector], []))
            indices = [len(items) - 1]
        for i in indices:
            items[i] = (items[i][0], [(p,v) for p,v in items[i][1] if p not in values])
        items[indices[-1]][1].extend(values.items())
    items, removed = prune(items)
    path.write_text('/* Owning panel rules. Shared colors come from MenuTheme.uss. */\n\n' + render(items), encoding='utf-8')
    print(name, 'removed overwritten declarations:', removed)

def surfaces(selectors):
    return {s: {'background-color': WHITE} for s in selectors}

edit('MenuCommon', {'.menu-panel': {'background-color': BG}, '.menu-detail-empty': {'flex-shrink':'0'}})
edit('MenuScreenHeader', {
    '.screen-header': {'background-color': BG, 'border-bottom-width':'1px', 'border-bottom-color':'#d8d5ca'},
    '.screen-header .screen-heading-mark': {'width':'24px', 'height':'24px', 'min-width':'24px', 'flex-shrink':'0', 'margin-left':'0', 'margin-top':'0', 'margin-bottom':'0', 'margin-right':'18px'},
})
edit('QuestPanel', {
    **surfaces(['.quest-journal-body .quest-list-column','.quest-journal-body .quest-list-scroll','.quest-detail-column','.quest-detail-scroll','.quest-detail-content','.objective-list','.quest-state-grid','.quest-card','.reward-row','.quest-extra-info-list']),
    '.quest-panel': {'background-color': BG},
    '.quest-journal-header': {'background-color': BG},
    '.quest-journal-body': {'background-color': BG},
    '.quest-detail-scroll': {'background-color':WHITE,'flex-grow':'1','flex-shrink':'1','flex-basis':'auto','min-width':'0','border-width':'0'},
    '.quest-detail-column': {'background-color':WHITE,'flex-direction':'column','min-height':'0'},
    '.quest-detail-column .menu-detail-empty': {'height':'100%','min-height':'0','flex-grow':'1','flex-shrink':'1','background-color':WHITE},
    '.quest-list-section-title': {'white-space':'normal'},
})
edit('InventoryPanel', {
    **surfaces(['.category-panel','.list-panel','.item-list','.item-row','.inventory-detail-column','.detail-panel','.detail-icon','.effect-box','.detail-effect','.inventory-item-card','.no-items-label']),
    '.inventory-panel': {'background-color':BG}, '.inventory-shell': {'background-color':BG},
    '.inventory-header': {'background-color':BG}, '.inventory-footer': {'background-color':BG},
})
edit('BoxPanel', {
    **surfaces(['.box-surface','.box-party-section','.box-selected-panel','.box-storage-panel','.box-party-card','.box-storage-card','.box-storage-info','.box-meta-item','.box-selected-portrait','.box-nature-card','.box-health-card','.box-stat-cell','.box-purchase-card']),
    '.box-panel': {'background-color':BG}, '.box-header': {'background-color':BG}, '.box-footer': {'background-color':BG},
    '.box-search-field .unity-text-input': {'background-color':WHITE},
    '.box-search-field .unity-base-text-field__input': {'background-color':WHITE, 'color':'#31573d','border-color':'#becfb4','padding':'8px'},
})
edit('ShopScreen', {**surfaces(['.shop-column','.shop-card','.shop-portrait-frame','.shop-portrait']), '.shop-panel': {'background-color':BG}})
edit('SettingsScreen', {**surfaces(['.settings-group','.settings-modal-card']), '.settings-panel': {'background-color':BG}})
edit('CodexPanel', {**surfaces(['.codex-column','.codex-progress','.codex-card','.codex-card-portrait','.codex-detail-portrait','.codex-evolution-icon']), '.codex-panel': {'background-color':BG}})
edit('PetsPanel', {
    **surfaces(['.pets-main-panel','.pets-preview-panel','.pets-information-panel','.pets-stats-card','.pet-party-card','.pet-card-portrait','.pet-skill-card','.pets-rename-dialog','.pets-rename-portrait']),
    '.pets-panel': {'background-color':BG,'padding-left':'0','padding-right':'0','padding-bottom':'0'},
    '.pets-panel .screen-header': {'margin-left':'0','margin-right':'0'},
    '.pets-party-section': {'padding-left':'18px','padding-right':'18px'},
    '.pets-main-panel': {'background-color':WHITE,'margin-left':'18px','margin-right':'18px','margin-bottom':'18px','flex-basis':'0'},
    '.pets-preview-panel': {'background-color':WHITE,'flex-basis':'38%','min-width':'0'},
    '.pets-selected-portrait': {'-unity-background-scale-mode':'scale-to-fit'},
    '.pets-information-panel': {'background-color':WHITE,'padding':'16px','flex-basis':'0'},
    '.pets-meta-row': {'height':'auto','min-height':'32px','flex-wrap':'wrap','overflow':'visible'},
    '.pets-meta-label': {'font-size':'14px','white-space':'normal','flex-shrink':'0'},
    '.pets-meta-separator': {'font-size':'14px'},
    '.pets-status-and-stats': {'height':'auto','min-height':'240px','flex-grow':'1','flex-basis':'0','flex-shrink':'0'},
    '.pets-status-bars': {'height':'64px','min-height':'64px'},
    '.pets-large-bar-row': {'height':'30px','min-height':'30px'},
    '.pets-large-bar-label': {'font-size':'14px','width':'36px','min-width':'36px'},
    '.pets-large-bar-value': {'font-size':'14px','width':'120px','min-width':'120px'},
    '.pets-stats-card': {'background-color':WHITE,'height':'auto','min-height':'180px','flex-grow':'1','padding':'10px'},
    '.pets-stats-columns': {'flex-direction':'row'},
    '.pets-stat-column': {'width':'50%','flex-basis':'0','flex-grow':'1','padding-right':'8px'},
    '.pets-stat-row': {'height':'auto','min-height':'30px','flex-grow':'1'},
    '.pets-stat-name': {'font-size':'14px','width':'105px','min-width':'105px'},
    '.pets-stat-value': {'font-size':'14px','width':'52px','min-width':'52px'},
    '.pets-skills-row': {'height':'94px','min-height':'94px'},
    '.pet-skill-card': {'height':'88px','min-height':'88px'},
    '.pet-skill-name': {'font-size':'15px','white-space':'normal'},
    '.pet-skill-meta': {'font-size':'12px'},
    '.pets-action-title': {'font-size':'18px'}, '.pets-action-subtitle': {'font-size':'12px'},
    '.pet-card-name': {'font-size':'15px'}, '.pet-card-hp-value': {'font-size':'12px'},
    '.pet-party-card': {'background-color':WHITE,'border-width':'2px'}, '.pet-party-card.is-selected': {'border-width':'2px','background-color':WHITE},
    '.pet-party-card.is-empty': {'background-color':WHITE},
    '.pet-party-card.is-summoned': {'background-color':WHITE},
    '.pet-party-card.is-summoned.is-selected': {'background-color':WHITE},
})

# User's follow-up: single-column stats, taller party cards, sticky action footer.
edit('PetsPanel', {
    '.pets-party-section': {'height':'auto','min-height':'0'},
    '.pets-party-row': {'height':'209px','min-height':'209px'},
    '.pet-party-card': {'height':'205px','min-height':'205px'},
    '.pets-status-and-stats': {'min-height':'294px','flex-grow':'0','flex-basis':'auto'},
    '.pets-stats-card': {'height':'230px','min-height':'230px','flex-grow':'0'},
    '.pets-stats-columns': {'flex-direction':'column'},
    '.pets-stat-column': {'width':'100%'},
    '.pets-stat-row': {'min-height':'22px'},
    '.pets-information-scroll': {'flex-grow':'1','flex-shrink':'1','flex-basis':'0','min-height':'0'},
    '.pets-information-scroll .unity-scroll-view__content-container': {'flex-grow':'1','min-height':'100%'},
    '.pets-panel.is-compact .pets-party-row': {'height':'168px','min-height':'168px'},
    '.pets-panel.is-compact .pet-party-card': {'height':'164px','min-height':'164px'},
    '.pets-panel.is-compact .pets-status-and-stats': {'min-height':'233px'},
    '.pets-panel.is-compact .pets-stats-card': {'height':'186px','min-height':'186px'},
    '.pets-panel.is-compact .pets-stat-row': {'min-height':'18px'},
})

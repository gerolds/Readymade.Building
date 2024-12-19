/* MIT License
 * Copyright 2023 Gerold Schneider
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the “Software”), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using System;
using System.Linq;
using Cysharp.Threading.Tasks;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using NaughtyAttributes;
#endif
using Readymade.Machinery.Acting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Readymade.Building.Components
{
    /// <summary>
    /// Generates the toolbar for a <see cref="Builder"/> and handling user input.
    /// </summary>
    [RequireComponent(typeof(Builder))]
    public class BuilderPresenter : MonoBehaviour
    {
        public static readonly int INVALID = -1;
        public static readonly (int, int) NOTHING_SELECTED = (INVALID, INVALID);

        [BoxGroup("Display")]
        [Tooltip("The zoom factor on the preview renders.")]
        [SerializeField]
        [Required]
        private float previewPadding = 1f;

        [BoxGroup("Display")]
        [Tooltip("The toolbar display to populate with the collections and tools of this builder.")]
        [SerializeField]
        [Required]
        private ToolbarDisplay toolbarDisplay;

        [BoxGroup("Display")]
        [Tooltip("The tool display prefab to use for the tools of this builder.")]
        [SerializeField]
        [Required]
        private ToolDisplay toolViewPrefab;

        [FormerlySerializedAs("tabViewPrefab")]
        [BoxGroup("Display")]
        [Tooltip("The tab display prefab use for the collections of this builder.")]
        [SerializeField]
        [Required]
        private TabDisplay collectionViewPrefab;

        [BoxGroup("Display")]
        [Tooltip("The tab display prefab use for the collections of this builder.")]
        [SerializeField]
        [Required]
        private TabDisplay groupViewPrefab;

        [BoxGroup("Display")]
        [Tooltip("The prefab to use for instantiating displays for individual resource cost items.")]
        [SerializeField]
        [Required]
        private PropCountDisplay propCountPrefab;

        [BoxGroup("Menu Control")]
        [SerializeField]
        private bool closeOnToolSelected = true;

        [Tooltip("The prop that identifies the primary cost of a placeable to display in the tool-view.")]
        [SerializeField]
        private SoProp primaryCostProp;

        [BoxGroup("Prefabs")] [SerializeField] private bool groupCollections;

        [SerializeField]
#if ODIN_INSPECTOR
        [ListDrawerSettings(ShowPaging = false, ShowFoldout = false)]
#else
        [ReorderableList]
#endif
        [BoxGroup("Prefabs")]
        [HideIf(nameof(groupCollections))]
        private SoPlaceableCollection[] collections;

        [SerializeField]
#if ODIN_INSPECTOR
        [ListDrawerSettings(ShowPaging = false, ShowFoldout = false)]
#else
        [ReorderableList]
#endif
        [BoxGroup("Prefabs")]
        [ShowIf(nameof(groupCollections))]
        private SoPlaceableGroup[] groups;

        [BoxGroup("Prefabs")]
        [Tooltip("The inventory component that holds the keys for unlocking placeables.")]
        [SerializeField]
        private InventoryComponent keys;

        private ToolDisplay[] _toolViews;
        private TabDisplay[] _collectionViews;
        private TabDisplay[] _groupViews;
        private ToggleGroup _toolToggleGroup;
        private ToggleGroup _collectionToggleGroup;
        private ToggleGroup _groupToggleGroup;
        private Builder _builder;

        private int _selectedGroup = INVALID;
        private int _selectedCollection = INVALID;
        private int _selectedTool = INVALID;
        private int _previouslySelectedTool = INVALID;
        private Placeable[] _prefabs;

        /// <summary>
        /// The previously selected tool.
        /// </summary>
        public int PreviouslySelectedTool => _previouslySelectedTool;

        /// <summary>
        /// The currently selected tool.
        /// </summary>
        public int SelectedTool => _selectedTool;

        /// <summary>
        /// The currently selected collection.
        /// </summary>
        public int SelectedCollection => _selectedCollection;

        /// <summary>
        /// All collections that are used by this builder.
        /// </summary>
        public SoPlaceableCollection[] Collections => collections;

        /// <summary>
        /// All groups that are used by this builder.
        /// </summary>
        public SoPlaceableGroup[] Groups => groups;

        /// <summary>
        /// Called whenever the selected tool, group or collection changes.
        /// </summary>
        public event Action CollectionChanged;

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnEnable()
        {
            _builder = GetComponent<Builder>();
            if (groupCollections)
            {
                groups = groups.Distinct().ToArray();
                collections = groups.SelectMany(it => it.Collections).Distinct().ToArray();
            }
            else
            {
                collections = collections.Distinct().ToArray();
            }

            OverrideCollections(collections);
            _builder.ToolChanged += ToolChangedHandler;
            keys.Modified += KeysChangedHandler;
            toolbarDisplay.ToolMenuToggle.Changed += ToolbarChangedHandler;
        }

        private void ToolbarChangedHandler(bool isOn)
        {
            if (!isOn && _toolToggleGroup)
            {
                _toolToggleGroup.SetAllTogglesOff(false);
            }
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Start()
        {
            GenerateToolbar();
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDisable()
        {
            _builder.ToolChanged -= ToolChangedHandler;
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void Update()
        {
            Builder.InputState input = _builder.Input;
            if (input.HotkeyThisFrame != INVALID && input.Version > 0)
            {
                if (input.ToolMenuIsOpen)
                {
                    SetCollection(_builder.Input.HotkeyThisFrame);
                }
                else
                {
                    // TODO: this should be a separate collection that is player-configurable
                    SetTool(input.HotkeyThisFrame);
                }

                UpdateFilters();
            }

            UpdateResourceDisplays(_toolViews, _prefabs, _builder.ResourceStockpile);
        }

        /// <summary>
        /// Event function.
        /// </summary>
        private void OnDestroy()
        {
            DestroyAllToolViews(in _toolViews, in _collectionViews);
        }

        public void OverrideCollections(SoPlaceableCollection[] items)
        {
            _prefabs = items.SelectMany(it => it.Placeables).Distinct().ToArray();
            _builder.SetCopyablePrefabs(_prefabs);
            _selectedTool = INVALID;
            _previouslySelectedTool = INVALID;
        }

        [Button]
        public void GenerateToolbar()
        {
            GenerateToolbar(
                out _toolViews,
                out _collectionViews,
                out _groupViews,
                out _toolToggleGroup,
                out _collectionToggleGroup,
                out _groupToggleGroup
            );
            UpdateResourceDisplays(_toolViews, _prefabs, _builder.ResourceStockpile);
            UpdateLockedDisplays(_toolViews, _prefabs, keys);
        }

        /// <summary>
        /// Activate a tool collection by ID.
        /// </summary>
        /// <param name="collectionID"></param>
        public void SetCollection(int collectionID)
        {
            if (collectionID < collections.Length && collectionID >= 0)
            {
                _selectedCollection = collectionID;
                if (_selectedTool >= 0 && _selectedTool < _prefabs.Length &&
                    !collections[_selectedCollection].Placeables.Contains(_prefabs[_selectedTool]))
                {
                    _selectedTool = INVALID;
                }

                _toolToggleGroup.SetAllTogglesOff(false);
                _collectionToggleGroup.SetAllTogglesOff(false);

                if (_selectedCollection != INVALID)
                {
                    _collectionViews[_selectedCollection].toggle.SetIsOnWithoutNotify(true);
                }

                if (_selectedTool != INVALID)
                {
                    _toolViews[_selectedTool].toggle.SetIsOnWithoutNotify(true);
                }

                if (groupCollections)
                {
                    SoPlaceableGroup group =
                        groups.First(it => it.Collections.Contains(collections[_selectedCollection]));
                    _selectedGroup = Array.IndexOf(groups, group);
                    _groupToggleGroup.SetAllTogglesOff(false);
                    if (_selectedGroup != INVALID)
                    {
                        _groupViews[_selectedGroup].toggle.SetIsOnWithoutNotify(true);
                    }
                }
            }
            else
            {
                _selectedGroup = INVALID;
                _selectedCollection = INVALID;
                _selectedTool = INVALID;
                _toolToggleGroup.SetAllTogglesOff(false);
                _collectionToggleGroup.SetAllTogglesOff(false);
                _groupToggleGroup.SetAllTogglesOff(false);
                CollectionChanged?.Invoke();
            }
        }

        /// <summary>
        /// Activate a tool group by ID.
        /// </summary>
        /// <param name="groupID">The group to activate.</param>
        /// <remarks>Note that groups are just an organisational unit and that fundamentally the selection of the group is made by
        /// selecting a collection or tool.</remarks>
        public void SetGroup(int groupID)
        {
            if (groupID < groups.Length && groupID >= 0)
            {
                SetCollection(Array.IndexOf(collections, groups[groupID].Collections[0]));
            }
            else
            {
                SetCollection(INVALID);
            }
        }

        /// <summary>
        /// Called whenever the selected tool of the builder changes.
        /// </summary>
        private void ToolChangedHandler()
        {
            if (closeOnToolSelected)
            {
                toolbarDisplay.ToolMenuToggle.SignalEnabled(false);
                _toolToggleGroup.SetAllTogglesOff(false);
            }
        }

        /// <summary>
        /// Called whenever the inventory holding unlock key changes.
        /// </summary>
        private void KeysChangedHandler(Phase message, IInventory<SoProp>.InventoryEventArgs args)
        {
            UpdateLockedDisplays(_toolViews, _prefabs, keys);
        }

        /// <summary>
        /// Updates the toolbar tab/group/tools to reflect the current selection.
        /// </summary>
        private void UpdateFilters()
        {
            // hide/show tools based on the selected collection
            for (int toolID = 0; toolID < _toolViews.Length; toolID++)
            {
                int collectionID = FindCollectionIDByToolID(toolID);
                bool isInSelectedCollection = collectionID == _selectedCollection; // && collectionID != INVALID;
                _toolViews[toolID].gameObject.SetActive(isInSelectedCollection);
            }

            if (groupCollections)
            {
                for (int groupID = 0; groupID < _groupViews.Length; groupID++)
                {
                    // activate the group tab
                    _groupViews[groupID].toggle.SetIsOnWithoutNotify(groupID == _selectedGroup);
                    _groupViews[groupID].gameObject.SetActive(true);
                }

                for (int collectionID = 0; collectionID < _collectionViews.Length; collectionID++)
                {
                    // hide all non-group collections
                    SoPlaceableCollection collection = collections[collectionID];
                    TabDisplay collectionView = _collectionViews[collectionID];
                    if (_selectedGroup != INVALID)
                    {
                        SoPlaceableGroup group = groups[_selectedGroup];
                        collectionView.gameObject.SetActive(group.Collections.Contains(collection));
                    }
                    else
                    {
                        collectionView.gameObject.SetActive(true);
                    }

                    // activate the collection tab
                    _collectionViews[collectionID].toggle.SetIsOnWithoutNotify(collectionID == _selectedCollection);
                }
            }
            else
            {
                for (int collectionID = 0; collectionID < _collectionViews.Length; collectionID++)
                {
                    // activate the collection tab
                    _collectionViews[collectionID].toggle.SetIsOnWithoutNotify(collectionID == _selectedCollection);
                    _collectionViews[collectionID].gameObject.SetActive(true);
                }
            }
        }

        /// <summary>
        /// Updates the toolbar based on resource availability.
        /// </summary>
        /// <param name="toolDisplays">A collection of displays for <see cref="Placeable"/> tools. Indices are expected to match <paramref name="tools"/></param>
        /// <param name="tools">A collection of <see cref="Placeable"/> tools. Indices are expected to match <paramref name="toolDisplays"/></param>
        /// <param name="resources">The resource system to query.</param>
        private void UpdateResourceDisplays(
            ToolDisplay[] toolDisplays,
            Placeable[] tools,
            IStockpile<SoProp> resources
        )
        {
            if (toolDisplays == null)
            {
                return;
            }

            for (int i = 0; i < tools.Length; i++)
            {
                ToolDisplay display = toolDisplays[i];
                bool isInteractive = !tools[i].PlacementCost
                    .Any(it => resources.GetAvailableCount(it.Identity) < it.Count);
                display.toolGroup.interactable = isInteractive;
                display.nonInteractable.SetActive(!isInteractive);
            }
        }

        /// <summary>
        /// Updates the toolbar based on the locked state of individual placeables.
        /// </summary>
        /// <param name="toolDisplays">A collection of displays for <see cref="Placeable"/> tools. Indices are expected to match <paramref name="tools"/></param>
        /// <param name="tools">A collection of <see cref="Placeable"/> tools. Indices are expected to match <paramref name="toolDisplays"/></param>
        /// <param name="unlockKeys">The keys inventory to query.</param>
        private void UpdateLockedDisplays(
            ToolDisplay[] toolDisplays,
            Placeable[] tools,
            IInventory<SoProp> unlockKeys
        )
        {
            if (toolDisplays == null)
            {
                return;
            }

            for (int i = 0; i < tools.Length; i++)
            {
                ToolDisplay display = toolDisplays[i];
                bool isUnlocked = unlockKeys == null || !tools[i].UnlockedBy.Any() ||
                    tools[i].UnlockedBy.All(it => !it || unlockKeys.GetAvailableCount(it) > 0);
                display.toolGroup.interactable = isUnlocked;
                display.toolGroup.alpha = isUnlocked ? 1f : 0.1f;
                display.nonInteractable.SetActive(!isUnlocked);
            }
        }

        /// <summary>
        /// Generates (fills) the toolbar with the configured collections and tools.
        /// </summary>
        /// <param name="toolViews">The generated tool views.</param>
        /// <param name="collectionViews">The generated collection views.</param>
        /// <param name="groupViews">The generated group views.</param>
        /// <param name="toolGroup">The toggle group for the tool views.</param>
        /// <param name="collectionGroup"></param>
        /// <param name="groupGroup"></param>
        private void GenerateToolbar(
            out ToolDisplay[] toolViews,
            out TabDisplay[] collectionViews,
            out TabDisplay[] groupViews,
            out ToggleGroup toolGroup,
            out ToggleGroup collectionGroup,
            out ToggleGroup groupGroup
        )
        {
            EnsureToolbarHasToggleGroup(out toolGroup);
            EnsureCollectionBarHasToggleGroup(out collectionGroup);
            EnsureGroupBarHasToggleGroup(out groupGroup);
            CreateToolViews(out toolViews, in toolGroup);
            CreateCollectionViews(out collectionViews, in collectionGroup);
            if (groupCollections)
            {
                CreateGroupViews(out groupViews, in groupGroup);
                groupGroup.SetAllTogglesOff();
            }
            else
            {
                groupViews = null;
            }

            collectionGroup.SetAllTogglesOff();
            toolGroup.SetAllTogglesOff();

            Debug.Log($"[{nameof(Builder)}] Generated toolbar", this);

            SetCollection(0);
            UpdateFilters();

            return;

            // create group tab views and setup event handlers
            void CreateGroupViews(out TabDisplay[] tabViews, in ToggleGroup toggleGroup)
            {
                tabViews = new TabDisplay[groups.Length];
                for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
                {
                    if (groups[groupIndex] == null)
                    {
                        continue;
                    }

                    TabDisplay view = Instantiate(groupViewPrefab, toolbarDisplay.GroupsLayout.transform);
                    view.toggle.group = toggleGroup;
                    view.SetIcon(groups[groupIndex].Icon);
                    view.label.SetText(groups[groupIndex].DisplayName);
                    view.index.SetText(groupIndex switch
                    {
                        9 => "0",
                        <= 8 => $"{groupIndex + 1}",
                        _ => ""
                    });
                    int indexCopy = groupIndex;
                    view.toggle.onValueChanged.AddListener(isOn => ToggleGroupHandler(indexCopy, isOn));
                    view.name = $"{nameof(TabDisplay)} [{view.index.text}] {groups[groupIndex].name}";
                    tabViews[groupIndex] = view;
                    if (tabViews[groupIndex].tipText)
                    {
                        tabViews[groupIndex].tipText.text = groups[groupIndex].Tooltip;
                    }

                    if (tabViews[groupIndex].tipLabel)
                    {
                        tabViews[groupIndex].tipLabel.text = groups[groupIndex].DisplayName;
                    }

                    if (tabViews[groupIndex].tipCanvas)
                    {
                        tabViews[groupIndex].tipCanvas.enabled = false;
                    }
                }
            }

            // create collection tab views and setup event handlers
            void CreateCollectionViews(out TabDisplay[] tabViews, in ToggleGroup toggleGroup)
            {
                tabViews = new TabDisplay[collections.Length];
                for (int collectionIndex = 0; collectionIndex < collections.Length; collectionIndex++)
                {
                    if (collections[collectionIndex] == null)
                    {
                        continue;
                    }

                    TabDisplay view = Instantiate(collectionViewPrefab, toolbarDisplay.CollectionsLayout.transform);
                    view.toggle.group = toggleGroup;
                    view.SetIcon(collections[collectionIndex].Icon);
                    view.label.SetText(collections[collectionIndex].DisplayName);
                    view.index.SetText(collectionIndex switch
                    {
                        9 => "0",
                        <= 8 => $"{collectionIndex + 1}",
                        _ => ""
                    });
                    int collectionIndexCopy = collectionIndex;
                    view.toggle.onValueChanged.AddListener(isOn =>
                        ToggleCollectionHandler(collectionIndexCopy, isOn));
                    view.name = $"{nameof(TabDisplay)} [{view.index.text}] {collections[collectionIndex].name}";
                    tabViews[collectionIndex] = view;
                    if (tabViews[collectionIndex].tipText)
                    {
                        tabViews[collectionIndex].tipText.text = collections[collectionIndex].Tooltip;
                    }

                    if (tabViews[collectionIndex].tipLabel)
                    {
                        tabViews[collectionIndex].tipLabel.text = collections[collectionIndex].DisplayName;
                    }

                    if (tabViews[collectionIndex].tipCanvas)
                    {
                        tabViews[collectionIndex].tipCanvas.enabled = false;
                    }
                }
            }

            // create tool views and setup event handlers
            void CreateToolViews(out ToolDisplay[] toolViews, in ToggleGroup toggleGroup)
            {
                toolViews = new ToolDisplay[_prefabs.Length];
                Texture2D[] prefabPreviewTextures = new Texture2D[_prefabs.Length];
                Sprite[] prefabPreviewSprites = new Sprite[_prefabs.Length];
                for (int prefabID = 0; prefabID < _prefabs.Length; prefabID++)
                {
                    Placeable prefab = _prefabs[prefabID];
                    PreviewGenerator.BackgroundColor = Color.clear;
                    PreviewGenerator.Padding = previewPadding;
                    PreviewGenerator.RenderSuperSampling = 2f;
                    prefabPreviewTextures[prefabID] = PreviewGenerator.GenerateModelPreview(
                        prefab.transform,
                        (int)(toolbarDisplay.ToolsLayout.cellSize.x * 4f),
                        (int)(toolbarDisplay.ToolsLayout.cellSize.y * 4f)
                    );
                    if (prefabPreviewTextures[prefabID] == null)
                    {
                        Debug.LogWarning(
                            $"{nameof(BuilderPresenter)} Failed to create preview for prefab {prefab?.name}",
                            prefab);
                        prefabPreviewTextures[prefabID] = Texture2D.grayTexture;
                    }

                    prefabPreviewSprites[prefabID] = Sprite.Create(
                        prefabPreviewTextures[prefabID],
                        new Rect(0, 0, prefabPreviewTextures[prefabID].width, prefabPreviewTextures[prefabID].height),
                        Vector2.one * 0.5f
                    );
                    ToolDisplay view = Instantiate(toolViewPrefab, toolbarDisplay.ToolsLayout.transform);
                    view.toggle.group = toggleGroup;
                    view.label.SetText(prefab.DisplayName);
                    PropCount primaryCost = prefab.PlacementCost.FirstOrDefault(it => it.Identity == primaryCostProp);
                    if (primaryCost.Identity != null)
                    {
                        view.cost.SetText($"{primaryCost.Count} {primaryCost.Identity.DisplayName}");
                    }
                    else
                    {
                        PropCount secondaryCost = prefab.PlacementCost.FirstOrDefault();
                        if (secondaryCost.Identity != null)
                        {
                            view.cost.SetText($"{secondaryCost.Count} {secondaryCost.Identity.DisplayName}");
                        }
                        else
                        {
                            view.cost.SetText("No cost");
                        }
                    }

                    view.index.SetText(prefabID switch
                    {
                        9 => "0",
                        <= 8 => $"{prefabID + 1}",
                        _ => string.Empty
                    });
                    view.preview.sprite = prefabPreviewSprites[prefabID];
                    int plCapture = prefabID;
                    int placeableID = prefabID;
                    view.toggle.onValueChanged.AddListener(isOn => ToggleToolHandler(placeableID, isOn));
                    view.name = $"{nameof(ToolDisplay)} [{view.index.text}] {prefab.name}";
                    toolViews[prefabID] = view;

                    // we use a shared infobox to display details about the selected tool; Since that tool is separate
                    // from the tool hierarchy we have to hook updates to that infobox up here. We do this by subscribing
                    // focus trigger events on the tool view. No unsub of these is needed because tool views will be
                    // destroyed when the builder is finalized.
                    view.focusTrigger.FocusGained += OnFocusGained;
                    view.focusTrigger.FocusLost += OnFocusLost;

                    continue; // remainder of this scope is for local functions only

                    void OnFocusLost()
                    {
                        // hide infobox
                        toolbarDisplay.SharedInfobox.Canvas.enabled = false;
                    }

                    void OnFocusGained()
                    {
                        // update infobox
                        toolbarDisplay.SharedInfobox.Preview.sprite = prefabPreviewSprites[plCapture];
                        toolbarDisplay.SharedInfobox.Description.text = prefab.Tooltip;
                        toolbarDisplay.SharedInfobox.Title.text = prefab.DisplayName;

                        // clear line items
                        for (int i = toolbarDisplay.SharedInfobox.Container.childCount - 1; i >= 0; i--)
                        {
                            Destroy(toolbarDisplay.SharedInfobox.Container.GetChild(i).gameObject);
                        }

                        foreach (PropCount prop in prefab.PlacementCost)
                        {
                            if (prop.Identity == null)
                            {
                                Debug.LogWarning($"[{nameof(Builder)}] Invalid cost definition in {prefab.name}",
                                    prefab);
                                continue;
                            }

                            // add new line items
                            PropCountDisplay propDisplay =
                                Instantiate(propCountPrefab, toolbarDisplay.SharedInfobox.Container);
                            propDisplay.Label.text = prop.Identity.DisplayName;
                            propDisplay.Count.SetText("{0}", prop.Count);
                            propDisplay.Annotation.SetText(String.Empty, prop.Count);
                        }

                        foreach (PropCount prop in prefab.DeletionCost)
                        {
                            if (prop.Identity == null)
                            {
                                Debug.LogWarning($"[{nameof(Builder)}] Invalid cost definition in {prefab.name}",
                                    prefab);
                                continue;
                            }

                            // add new line items
                            PropCountDisplay propDisplay =
                                Instantiate(propCountPrefab, toolbarDisplay.SharedInfobox.Container);
                            propDisplay.Label.text = prop.Identity.Name;
                            propDisplay.Count.SetText("{0}", prop.Count);
                            propDisplay.Annotation.SetText(String.Empty, prop.Count);
                        }

                        toolbarDisplay.SharedInfobox.Canvas.enabled = true;
                    }
                }


                toolbarDisplay.SharedInfobox.Canvas.enabled = false;
            }
        }

        // Ensure that the toolbar has a toggle group
        private void EnsureToolbarHasToggleGroup(out ToggleGroup toggleGroup)
        {
            if (!toolbarDisplay.ToolsLayout.TryGetComponent(out toggleGroup))
            {
                toggleGroup = toolbarDisplay.ToolsLayout.gameObject.AddComponent<ToggleGroup>();
            }

            toggleGroup.allowSwitchOff = true;
        }

        // Ensure that the collection bar has a toggle group
        private void EnsureCollectionBarHasToggleGroup(out ToggleGroup toggleGroup)
        {
            if (!toolbarDisplay.CollectionsLayout.TryGetComponent(out toggleGroup))
            {
                toggleGroup = toolbarDisplay.CollectionsLayout.gameObject.AddComponent<ToggleGroup>();
            }

            toggleGroup.allowSwitchOff = false;
        }

        // Ensure that the group bar has a toggle group
        private void EnsureGroupBarHasToggleGroup(out ToggleGroup toggleGroup)
        {
            if (!toolbarDisplay.GroupsLayout.TryGetComponent(out toggleGroup))
            {
                toggleGroup = toolbarDisplay.GroupsLayout.gameObject.AddComponent<ToggleGroup>();
            }

            toggleGroup.allowSwitchOff = false;
        }

        /// <summary>
        /// Clears all tool views.
        /// </summary>
        /// <param name="toolViews"></param>
        /// <param name="tabDisplays"></param>
        private void DestroyAllToolViews(in ToolDisplay[] toolViews, in TabDisplay[] tabDisplays)
        {
            for (int i = toolViews.Length - 1; i >= 0; i--)
            {
                // we need to check if this object still exists else we get an NRE on domain unload
                if (toolViews[i])
                {
                    Destroy(toolViews[i].gameObject);
                }
            }

            for (int i = tabDisplays.Length - 1; i >= 0; i--)
            {
                // we need to check if this object still exists else we get an NRE on domain unload
                if (tabDisplays[i])
                {
                    Destroy(tabDisplays[i].gameObject);
                }
            }
        }

        /// <summary>
        /// Called whenever a tool was toggled.
        /// </summary>
        public void ToggleToolHandler(int tool, bool isOn)
        {
            if (isOn)
            {
                SetTool(tool);
            }
            else
            {
                if (!_toolToggleGroup.AnyTogglesOn())
                {
                    SetTool(INVALID);
                }
            }

            UpdateFilters();
        }

        /// <summary>
        /// Called whenever a collection-tab was toggled.
        /// </summary>
        private void ToggleCollectionHandler(int collection, bool isOn)
        {
            if (isOn)
            {
                SetCollection(collection);
                UpdateFilters();
            }
            else
            {
                if (!_collectionToggleGroup.AnyTogglesOn())
                {
                    SetCollection(INVALID);
                    UpdateFilters();
                }
            }

            // BUG: this causes the toggle to become unresponsive to event system input, as it resets the toogle's effect.
        }

        /// <summary>
        /// Called whenever a group-tab was toggled.
        /// </summary>
        private void ToggleGroupHandler(int groupID, bool isOn)
        {
            if (isOn)
            {
                SetGroup(groupID);
                UpdateFilters();
            }
            else
            {
                if (!_groupToggleGroup.AnyTogglesOn())
                {
                    SetGroup(INVALID);
                    UpdateFilters();
                }
            }
        }

        /// <summary>
        /// Activate a tool by ID.
        /// </summary>
        /// <param name="tool">ID of the tool to activate. ID corresponds to the index of the tool in <see cref="_prefabs"/> and <see cref="_toolViews"/>.</param>
        public void SetTool(int tool)
        {
            if (CheckToolID(tool))
            {
                Debug.Log($"[{nameof(Builder)}] Selecting tool ID {tool}", this);
                // we do not change the selected collection, as this here is either called as part of a click on a toggle
                // in a collection or a hotkey, in neither case do we want to change the state of the menu. Instead we
                // prefer to maintain it so the user can return to the position where he left off.

                _previouslySelectedTool = _selectedTool;
                _selectedTool = tool;
                if (TryFindCollectionIDByToolID(tool, out var collectionID))
                {
                    SetCollection(collectionID);
                }

                // this will trigger the tool changed event on the builder which we subscribe.
                _builder.SetTool(_prefabs[tool]);
            }
            else
            {
                Debug.Log($"[{nameof(Builder)}] Invalid tool ID {tool}, selection cancelled.", this);
                _builder.Cancel();
            }
        }

        /// <summary>
        /// Checks whether the given tool ID is valid, i.e. that it is within the bounds of the prefabs array and not undefined.
        /// </summary>
        private bool CheckToolID(int tool) => tool != INVALID && tool >= 0 && tool < _prefabs.Length && _prefabs[tool];

        private bool TryFindCollectionIDByToolID(int toolID, out int collectionID)
        {
            if (TryFindCollectionByToolID(toolID, out var collection))
            {
                collectionID = Array.IndexOf(collections, collection);
                return collectionID != -1;
            }

            collectionID = INVALID;
            return false;
        }

        /// <summary>
        /// Finds the ID of the collection that contains the given tool.
        /// </summary>
        /// <param name="toolID">The tool ID to search for.</param>
        /// <returns>ID of the collection that contains the tool. -1 if the tool was not found.</returns>
        /// <exception cref="InvalidOperationException">When the tool was not found in any collection.</exception>
        /// <exception cref="ArgumentNullException">When the tool is undefined.</exception>
        /// <exception cref="ArgumentOutOfRangeException">When the tool ID does not exist.</exception>
        private int FindCollectionIDByToolID(int toolID)
            => Array.IndexOf(collections, FindCollectionByToolID(toolID));


        /// <summary>
        /// Finds the collection that contains the given tool.
        /// </summary>
        /// <param name="toolID">The tool ID to search for.</param>
        /// <param name="collection">The collection containing the tool.</param>
        /// <returns>Whether the tool was found in any collection.</returns>
        private bool TryFindCollectionByToolID(int toolID, out SoPlaceableCollection collection)
        {
            collection = null;
            if (_prefabs.Length <= toolID)
            {
                return false;
            }

            Placeable prefab = _prefabs[toolID];
            collection = collections.FirstOrDefault(it => it.Contains(prefab));
            return collection;
        }

        /// <summary>
        /// Finds the collection that contains the given tool.
        /// </summary>
        /// <param name="toolID">The tool ID to search for.</param>
        /// <returns>The collection containing the tool.</returns>
        /// <exception cref="InvalidOperationException">When the tool was not found in any collection.</exception>
        /// <exception cref="ArgumentNullException">When the tool is undefined.</exception>
        /// <exception cref="ArgumentOutOfRangeException">When the tool ID does not exist.</exception>
        private SoPlaceableCollection FindCollectionByToolID(int toolID)
            => collections.First(it => it.Contains(_prefabs[toolID]));

        /// <summary>
        /// Checks if the given selection is valid. 
        /// </summary>
        /// <param name="value">The selected collection and prefab.</param>
        /// <returns>Whether the check succeeded.</returns>
        public bool IsValidSelection((int collectionID, int placeableID) value)
        {
            return value.collectionID >= 0 &&
                value.collectionID < collections.Length &&
                value.placeableID >= 0 &&
                value.placeableID < collections[value.collectionID].Placeables.Count
                && collections[value.collectionID].Placeables[value.placeableID] != null;
        }
    }
}
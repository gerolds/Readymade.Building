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

using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Readymade.Building.Components
{
    /// <summary>
    /// A display that holds component references for displaying a tool.
    /// </summary>
    public class ToolDisplay : MonoBehaviour
    {
        [Tooltip("The image to display a rendered preview or icon for the tool.")]
        [SerializeField]
        [Required]
        public Image preview;

        [Tooltip("The toggle that captures input events and displays feedback.")]
        [SerializeField]
        [Required]
        public Toggle toggle;

        [Tooltip("The text for displaying the name of the tool.")]
        [SerializeField]
        [Required]
        public TMP_Text label;

        [Tooltip("The text for displaying the cost of using the tool.")]
        [SerializeField]
        [Required]
        public TMP_Text cost;

        [Tooltip("The text for displaying the sibling index of the tool. Could be used as hotkey ID.")]
        [SerializeField]
        [Required]
        public TMP_Text index;

        [Tooltip("The canvas group that can be used to control visibility and interactivity of the tool display.")]
        [SerializeField]
        [Required]
        public CanvasGroup toolGroup;

        [Tooltip("The focus trigger component that can be used to observe select and hover events.")]
        [SerializeField]
        [Required]
        public EventSystemFocusTrigger focusTrigger;

        [Tooltip("The GameObject to activate while the tool is not selectable (for any reason)")]
        [SerializeField]
        [Required]
        public GameObject nonInteractable;
    }
}
// using Microsoft.AspNetCore.Components.RenderTree;
// using Yoga;
// using BlazorBindings.Brutalist.Elements;
// using System;
// using System.Collections.Generic;

// namespace BlazorBindings.Brutalist;



// public class YogaElementAdapter
// {
//     private readonly Element _parent;

//     public YogaElementAdapter(Element parent)
//     {
//         _parent = parent ?? throw new ArgumentNullException(nameof(parent));
//     }

//     public void ApplyEdits(RenderTreeEdit[] edits, RenderTreeFrame[] frames)
//     {
//         var parentStack = new Stack<Element>();
//         var currentParent = _parent;

//         foreach (var edit in edits)
//         {
//             switch (edit.Type)
//             {
//                 case RenderTreeEditType.StepIn:
//                     var newParent = ResolveElement(frames[edit.ReferenceFrameIndex]);
//                     parentStack.Push(currentParent);
//                     currentParent = newParent;
//                     break;

//                 case RenderTreeEditType.StepOut:
//                     currentParent = parentStack.Pop();
//                     break;

//                 case RenderTreeEditType.PrependFrame:
//                     var prependFrame = frames[edit.ReferenceFrameIndex];
//                     AddFrameAsChild(currentParent, prependFrame, 0);
//                     break;

//                 // case RenderTreeEditType.InsertFrame:
//                 //     var insertFrame = frames[edit.ReferenceFrameIndex];
//                 //     AddFrameAsChild(currentParent, insertFrame, edit.SiblingIndex);
//                 //     break;

//                 case RenderTreeEditType.RemoveFrame:
//                     RemoveChild(currentParent, edit.SiblingIndex);
//                     break;

//                 case RenderTreeEditType.SetAttribute:
//                     ApplyAttribute(currentParent, frames[edit.ReferenceFrameIndex]);
//                     break;
//             }
//         }
//     }

//     // private Element ResolveElement(RenderTreeFrame frame)
//     // {
//     //     if (frame.FrameType == RenderTreeFrameType.Element)
//     //     {
//     //         return new YogaElement(frame.ElementName);
//     //     }
//     //     else if (frame.FrameType == RenderTreeFrameType.Component)
//     //     {
//     //         return new ComponentWrapperElement(frame.ComponentType);
//     //     }
//     //     throw new NotSupportedException($"Unsupported frame type: {frame.FrameType}");
//     // }

//     // private void AddFrameAsChild(Element parent, RenderTreeFrame frame, int index)
//     // {
//     //     Element child = frame.FrameType switch
//     //     {
//     //         RenderTreeFrameType.Element => new YogaElement(frame.ElementName),
//     //         RenderTreeFrameType.Text => new TextElement(frame.TextContent),
//     //         RenderTreeFrameType.Component => new ComponentWrapperElement(frame.ComponentType),
//     //         _ => throw new NotSupportedException($"Unsupported frame type: {frame.FrameType}")
//     //     };

//     //     parent.InsertChild(index, child);
//     // }

//     // private void RemoveChild(Element parent, int index)
//     // {
//     //     parent.RemoveChild(index);
//     // }

//     // private void ApplyAttribute(Element element, RenderTreeFrame attributeFrame)
//     // {
//     //     if (attributeFrame.FrameType != RenderTreeFrameType.Attribute)
//     //         return;

//     //     var name = attributeFrame.AttributeName;
//     //     var value = attributeFrame.AttributeValue;

//     //     element.ApplyAttribute(name, value);
//     // }
// }

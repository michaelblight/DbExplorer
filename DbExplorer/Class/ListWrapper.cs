using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Data;
using System.Collections;

namespace DbExplorer.Class
{
    public class ListWrapper : ICustomTypeDescriptor
    {
        private IList list;

        public ListWrapper(IList List)
        {
            list = List;
        }

        public String GetClassName()
        {
            return TypeDescriptor.GetClassName(this, true);
        }

        public AttributeCollection GetAttributes()
        {
            return TypeDescriptor.GetAttributes(this, true);
        }

        public String GetComponentName()
        {
            return TypeDescriptor.GetComponentName(this, true);
        }

        public TypeConverter GetConverter()
        {
            return TypeDescriptor.GetConverter(this, true);
        }

        public EventDescriptor GetDefaultEvent()
        {
            return TypeDescriptor.GetDefaultEvent(this, true);
        }

        public PropertyDescriptor GetDefaultProperty()
        {
            return TypeDescriptor.GetDefaultProperty(this, true);
        }

        public object GetEditor(Type editorBaseType)
        {
            return TypeDescriptor.GetEditor(this, editorBaseType, true);
        }

        public EventDescriptorCollection GetEvents(Attribute[] attributes)
        {
            return TypeDescriptor.GetEvents(this, attributes, true);
        }

        public EventDescriptorCollection GetEvents()
        {
            return TypeDescriptor.GetEvents(this, true);
        }

        public object GetPropertyOwner(PropertyDescriptor pd)
        {
            return this;
        }

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            return GetProperties();
        }

        public PropertyDescriptorCollection GetProperties()
        {
            PropertyDescriptorCollection pds = new PropertyDescriptorCollection(null);
            foreach (object item in list)
            {
                ListItemPropertyDescriptor pd = new ListItemPropertyDescriptor(item);
                pds.Add(pd);
            }
            return pds;
        }
    }

    public class ListItemPropertyDescriptor : PropertyDescriptor
    {
        object item = null;

        public ListItemPropertyDescriptor(object item)
            : base(item.ToString(), null)
        {
            this.item = item;
        }

        public override AttributeCollection Attributes
        {
            get
            {
                return new AttributeCollection(null);
            }
        }

        public override bool CanResetValue(object component)
        {
            return true;
        }

        public override Type ComponentType
        {
            get
            {
                return this.item.GetType();
            }
        }

        public override string DisplayName
        {
            get
            {
                return item.ToString();
            }
        }

        public override object GetValue(object component)
        {
            return this.item;
        }

        public override bool IsReadOnly
        {
            get { return true; }
        }

        public override string Name
        {
            get { return item.ToString(); }
        }

        public override Type PropertyType
        {
            get { return this.item.GetType(); }
        }

        public override void ResetValue(object component) { }

        public override bool ShouldSerializeValue(object component)
        {
            return true;
        }

        public override void SetValue(object component, object value)
        {
            // this.collection[index] = value;
        }
    }

}

using System.ComponentModel;
using static SPIRVCross.SPIRV;

namespace SPIRVCross;

public static class Extensions {

    public static int GetNumMemberTypes(this spvc_type type)
        => (int)spvc_type_get_num_member_types(type);

    public static uint GetMemberTypeID(this spvc_type type, int index)
        => spvc_type_get_member_type(type, (uint) index);

    public static int GetNumArrayDimensions(this spvc_type type)
        => (int) spvc_type_get_num_array_dimensions(type);

    public static int GetArrayDimension(this spvc_type type, int index)
        => (int) spvc_type_get_array_dimension(type, (uint) index).Value;

    public static spvc_basetype GetBasetype(this spvc_type type)
        => spvc_type_get_basetype(type);

    public static int GetVecRows(this spvc_type type)
        => (int) spvc_type_get_vector_size(type);

    public static int GetVecCols(this spvc_type type)
        => (int) spvc_type_get_columns(type);

}
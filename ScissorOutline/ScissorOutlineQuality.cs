using System.ComponentModel.DataAnnotations;

namespace ScissorOutline;

public enum ScissorOutlineQuality
{
    [Display(Name = nameof(Texts.QualityBalanced), Description = nameof(Texts.QualityBalancedDescription), ResourceType = typeof(Texts))]
    Balanced,
    [Display(Name = nameof(Texts.QualityHigh), Description = nameof(Texts.QualityHighDescription), ResourceType = typeof(Texts))]
    High,
    [Display(Name = nameof(Texts.QualityUltra), Description = nameof(Texts.QualityUltraDescription), ResourceType = typeof(Texts))]
    Ultra,
}

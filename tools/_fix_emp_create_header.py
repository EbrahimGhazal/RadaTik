from pathlib import Path

p = Path(r"d:\SkyBeam\MyApp\RadTik\RadTik_20260225_Full_01\RadaTik\Areas\CompanyEmployee\Views\Receiver\Create.cshtml")
text = p.read_text(encoding="utf-8")
old = (
    '<div class="radtk-page--companyemployee-views-receiver-create">\n'
    '<div class="radtk-page--companyemployee-views-receiver-create">\n'
    "@model RadaTik.Models.Receiver\n"
    "@using RadaTik.ViewModels\n"
)
new = (
    "@model RadaTik.Models.Receiver\n"
    "@using RadaTik.ViewModels\n"
    '<div class="radtk-page--companyemployee-views-receiver-create">\n'
    '<div class="radtk-page--companyemployee-views-receiver-create">\n'
)
if old not in text:
    raise SystemExit("pattern not found:\n" + repr(text[:250]))
p.write_text(text.replace(old, new, 1), encoding="utf-8")
print("ok")

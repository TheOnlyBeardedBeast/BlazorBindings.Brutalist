const fs = require("fs");
const path = require("path");

// Read the JSON file
const jsonPath = path.join(__dirname, "selection.json");
const data = JSON.parse(fs.readFileSync(jsonPath, "utf8"));

// Generate enum
let enumCode = "namespace BlazorBindings.Brutalist.Phosphor\n";
enumCode += "{\n";
enumCode += "    public enum PhosphorIcon\n";
enumCode += "    {\n";

data.icons.forEach((icon, index) => {
  const name = icon.properties.name;
  const code = icon.properties.code;

  // Convert kebab-case to PascalCase
  const pascalName = name
    .split("-")
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join("");

  enumCode += `        ${pascalName} = ${code},\n`;
});

enumCode += "    }\n";
enumCode += "}\n";

// Write to file
const outputPath = path.join(__dirname, "PhosphorIcon.cs");
fs.writeFileSync(outputPath, enumCode, "utf8");

console.log(`✓ Generated PhosphorIcon.cs with ${data.icons.length} icons`);
